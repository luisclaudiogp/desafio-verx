# ADR-0015 — Regra de saldo suficiente e hash da requisição na idempotência

- **Status:** Aceito
- **Data:** 2026-10-20

## Contexto
Duas lacunas do desenho original ([ADR-0005](0005-ledger-imutavel-estorno-idempotency-key.md)):

1. **Nada impedia o saldo de ficar negativo.** Um débito sem saldo era aceito, e o Consolidado só "descobria" depois (consistência eventual).
   Em um banco, essa decisão tem de ser tomada **antes** de gravar, de forma forte, na escrita.
2. **A `Idempotency-Key` não olhava o corpo.** Reutilizar a mesma chave com outro valor devolvia o lançamento original como se fosse
   um replay, escondendo um erro do cliente (o valor enviado nunca foi registrado).

## Decisão
### Saldo suficiente
- O agregado **`PosicaoConta`** (Domain do Lançamentos) guarda o saldo corrente da conta e decide: `Aplicar(lancamento)` recusa um
  **débito maior que o saldo** com `SaldoInsuficienteException` (`422`). Crédito sempre soma.
- Os colaboradores têm responsabilidade única: `VerificadorDeReplay` (chave + hash → replay ou `422`) e `LancamentoPersistidor` (posição, lançamento e evento em uma transação). Os *handlers* só orquestram.
- A posição é gravada **na mesma transação** do lançamento e do outbox (`posicoes_contas`, `numeric(18,2)`).
- **Concorrência (padrão: otimista, EF):** a posição tem `xmin` como *token* de concorrência; se outra transação a alterou, o `UPDATE` não atinge a linha, o persistidor repete a operação (relendo o saldo) e, esgotadas as tentativas, a API responde `409`. Isso garante "nunca aceitar débito sem saldo" sem trava. Para contas de alta contenção há o modo `Posicao:Modo = Sql` ([ADR-0019](0019-ef-por-padrao-sql-como-otimizacao-medida.md)):
- **Concorrência (modo SQL):** o repositório faz `SELECT ... FOR UPDATE` da linha da conta, então lançamentos da **mesma conta** se serializam e
  de contas diferentes correm em paralelo. Se a linha não existe, ela é criada com `INSERT ... ON CONFLICT DO NOTHING` e relida.
- **Atomicidade:** `IUnitOfWork.ExecutarAsync` abre a transação, executa a operação, salva e confirma; em qualquer exceção ela é
  desfeita e o *change tracker* é limpo. Débito recusado ⇒ nada é gravado (nem lançamento, nem evento).
- **Estorno nunca é bloqueado.** Um estorno de crédito já gasto pode deixar o saldo negativo, que fica visível para tratamento (é a
  consequência contábil correta de desfazer um crédito).
- A migration cria a tabela e faz o *backfill* a partir do ledger existente.

### Hash da requisição
- O lançamento guarda `HashRequisicao` = SHA-256 de `tipo | valor (F2) | ocorridoEm (UTC)` (estorno: do `lancamentoId` estornado),
  calculado por `HashDaRequisicao` (Domain).
- Mesma chave + mesmo hash ⇒ **replay** (`200`, `Idempotent-Replayed: true`). Mesma chave + hash diferente ⇒
  `ChaveIdempotenciaReutilizadaException` (`422`). Vale também na corrida entre duas requisições simultâneas.
- **Dados anteriores:** a migration recalcula o hash dos lançamentos existentes em SQL (mesma fórmula do domínio, verificada por teste que migra em duas etapas a partir de dados da versão anterior), então o domínio compara o hash **sempre** de forma estrita, sem exceção para "legado". Limite conhecido: uma requisição antiga enviada sem `ocorridoEm` não reproduz o hash e, se reenviada, recebe `422`.

## Alternativas consideradas
- **Calcular o saldo somando o ledger a cada débito** — correto, mas O(n) por conta e ainda exige trava para ser seguro.
- **Controle otimista (`xmin`) na posição** — sob conta quente gera retries em cascata (o mesmo problema que medimos na projeção, ADR-0012).
- **`UPDATE ... WHERE saldo >= valor` direto no SQL** — atômico e barato, mas esconde a regra no banco; preferi a regra no **domínio**,
  testável sem infraestrutura, e a trava só para serializar.
- **Consultar o Consolidado para validar o saldo** — acopla os serviços e usa dado eventual; rejeitado.
- **Cheque especial / limite por conta** — fora do escopo; o ponto de extensão é `PosicaoConta.Aplicar` (acrescentar `Limite`).

## Consequências
- (+) Invariante financeira garantida na fonte da verdade, provada em teste de integração: 10 débitos de 30 em paralelo sobre 100 ⇒
  exatamente 3 aceitos, 7 recusados, saldo 10, e `posição == soma do ledger` também em 40 operações mistas concorrentes.
- (+) Chave reutilizada com corpo diferente deixa de passar silenciosamente.
- (−) Em **contas muito disputadas**, o padrão (concorrência otimista) gera repetições e, ao esgotá-las, `409`; o modo `FOR UPDATE` serializa com custo previsível. Medido com 32 operações em paralelo: 10 contas ⇒ 13,7 % de `409`; 1 conta ⇒ ~72 % (no k6, 200 req/s numa conta ⇒ 5,9 %); com `FOR UPDATE`, 0 % ([benchmark](../benchmarks/ef-vs-sql.md)).
  uma conta (≈200 req/s nela) ⇒ p95 de 11,5 ms. Custo geral a 800 req/s: p95 de 14 para 16 ms.
- (−) Há duas visões de saldo: a **forte** (`posicoes_contas`, escrita) e a **eventual** (Consolidado, leitura). Um job de reconciliação
  entre as duas é a evolução natural.
