# ADR-0019 — EF Core por padrão; SQL específico como otimização opcional e medida

- **Status:** Aceito (refina os [ADR-0012](0012-projecao-em-lote-e-acumulacao-atomica.md) e [ADR-0015](0015-saldo-suficiente-e-hash-da-requisicao.md))
- **Data:** 2026-10-20

## Contexto
Os pontos de escrita mais disputados usavam SQL específico do PostgreSQL escrito à mão: `INSERT ... ON CONFLICT` com `unnest` na projeção do
Consolidado e `SELECT ... FOR UPDATE` na posição de saldo do Lançamentos. Funciona e é rápido, mas tem custos: o SQL em *string* não é checado pelo
compilador nem sobrevive a refatorações, prende o sistema ao PostgreSQL e é código que o time precisa saber ler. A pergunta justa: **o EF Core resolve
sozinho, e quanto custa não usar o SQL?**

## Decisão
**O padrão é o EF Core. O SQL otimizado continua disponível, por configuração, e só se justifica com medição.**

| Ponto | Padrão (EF) | Opcional (SQL) | Chave |
|---|---|---|---|
| Projeção do Consolidado | `AddRange`/`SaveChanges` + `ExecuteUpdate` por delta; conflito de chave ou *deadlock* ⇒ `ConcorrenciaException` ⇒ o *handler* **repete o lote** (até 3 vezes) | upsert em lote com `unnest` | `Projecao:Modo` |
| Posição de saldo | concorrência otimista (`xmin`); conflito ⇒ o persistidor **repete a operação** (até 5 vezes, espera crescente com variação) | `FOR UPDATE` | `Posicao:Modo` |
| Saldo em um instante (leitura) | consultas LINQ (`AsNoTracking`) | — | — |

`docker compose -f docker-compose.yml -f docker-compose.sql.yml up -d --build` sobe os dois serviços no modo SQL. Os dois modos têm o mesmo contrato
(`IProjecaoTransacao`, `IPosicaoContaRepository`) e a **mesma bateria de testes de integração** roda contra ambos (PostgreSQL real).

**Continuam em SQL, por necessidade** (o EF não expressa): o relay do Outbox (`FOR UPDATE SKIP LOCKED`), a geração de snapshots (função de janela e
*advisory lock*) e a correção de snapshots de eventos atrasados.

## Por que o padrão EF é seguro
- **Correção não depende do modo.** A concorrência otimista nunca aceita um débito sem saldo: se outra transação alterou a posição, o `UPDATE ... WHERE xmin = @lido`
  não atinge nenhuma linha, a operação é repetida e relê o saldo. Medido: em todos os experimentos a posição igualou o ledger e nenhum saldo ficou negativo.
- **O conflito é tratado, não ignorado.** O "EF ingênuo" (sem tratamento) falhou em ~87 % dos lotes concorrentes na primeira escrita de cada (conta, dia);
  o EF com tratamento teve **0 falhas definitivas** (os lotes conflitantes são repetidos).
- **Sem custo quando não há conflito:** o EF com tratamento custou o mesmo que o ingênuo (27 ms contra 27 ms para 500 eventos).

## O que o padrão EF custa (medido; ver o [benchmark](../benchmarks/ef-vs-sql.md))
- **Projeção:** de 1,7× a 12× mais lenta que o upsert em lote, e ~5–7× quando muitas contas criam o saldo do dia ao mesmo tempo (virada do dia UTC). O ganho do SQL
  vem do **número de idas ao banco**, não de o SQL ser mais rápido.
- **Conta quente:** com muitas escritas simultâneas na **mesma conta**, a concorrência otimista gera repetições e, ao esgotá-las, `409`. Medido com 32 em paralelo:
  10 contas ⇒ 13,7 % de `409`; 1 conta ⇒ ~72 %; no k6, 200 req/s numa só conta ⇒ 5,9 %. Com `FOR UPDATE`: 0 %.
- **Carga espalhada (800 req/s em 200 contas):** equivalente (p95 de 14–23 ms no EF e 12–20 ms no SQL, 0 % de erro).

## Quando trocar para SQL
- `Posicao:Modo = Sql`: contas de **alta contenção** (conta de recebimento de um grande lojista, conta de liquidação). Serializa com custo previsível.
- `Projecao:Modo = Sql`: quando o volume de eventos por segundo ou a **virada do dia** pressionarem a latência da projeção.
- Tudo é por configuração, sem mudança de código nem de contrato.

## Alternativas consideradas
- **Manter SQL como padrão** — mais rápido, mas é a opção que prende ao PostgreSQL e que mais exige explicação; a decisão passou a ser *medir antes de pagar esse preço*.
- **Só EF, sem o modo SQL** — mais simples, porém deixaria sem saída as contas quentes e a virada do dia.
- **Trava por conta em memória (um semáforo por conta) para reduzir os conflitos do EF** — só ajuda dentro de uma instância; com várias réplicas o problema volta.
- **EF + `ExecuteUpdate` sem tratamento de conflito** — o "ingênuo" medido: perde ~87 % dos lotes na primeira escrita concorrente.
- **Trocar de banco (por exemplo, SQL Server)** — não elimina o problema (atualização perdida e "verificar e depois inserir" valem em qualquer SGBD); muda só a ferramenta
  (`MERGE ... HOLDLOCK`, `UPDLOCK`). O modo SQL hoje é específico de PostgreSQL, isolado atrás das interfaces.

## Consequências
- (+) O caminho padrão não tem SQL escrito à mão (exceto o que o EF não faz), é portável e checado pelo compilador.
- (+) A escolha por SQL fica documentada, medida e reversível por configuração.
- (−) O padrão EF é mais lento na projeção e **expõe `409` em contas muito disputadas**; o cliente precisa repetir com a mesma `Idempotency-Key`.
- (−) Dois modos significam duas implementações a manter e a testar (a mesma bateria de integração cobre as duas).
- (−) O EF registra os conflitos de chave tratados como erro; as categorias `Microsoft.EntityFrameworkCore.Update` e `...Database.Command` estão silenciadas em
  `appsettings.json` para não gerar ruído. Falhas reais continuam sendo registradas pelo tratamento de exceções e pelo MassTransit.
