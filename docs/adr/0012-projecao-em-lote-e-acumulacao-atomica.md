# ADR-0012 — Projeção em lote com acumulação atômica

- **Status:** Aceito — substitui o tratamento de concorrência do consumidor descrito no [ADR-0004](0004-consistencia-idempotencia-concorrencia.md)
- **Data:** 2026-10-19

## Contexto
O consumidor original processava **um evento por vez**: três consultas, uma gravação e um `commit` por mensagem, mais um *retry* sempre que dois
consumidores disputavam o mesmo saldo (concorrência otimista com `xmin`). Isso limita a vazão ao tempo de ida e volta ao banco e piora justamente
sob alta contenção (contas muito movimentadas) — o ADR-0004 já listava o upsert atômico como a otimização para esse caso.

## Decisão
O consumo passa a ser **em lote** (`IConsumer<Batch<LancamentoCriadoEvent>>`: até 500 mensagens ou 200 ms, 4 lotes simultâneos) e o handler
(`RegistrarMovimentosEmLoteHandler`) faz, **em uma única transação e com poucas idas ao banco por lote**:

1. **Valida cada item** pelo domínio (`Movimento.Criar`). Item inválido não derruba o lote: vai para a tabela de quarentena
   `eventos_rejeitados` (payload + motivo), auditável e reprocessável.
2. **Insere os movimentos** com `INSERT ... SELECT unnest(...) ON CONFLICT ("LancamentoId") DO NOTHING RETURNING "LancamentoId"`.
   A chave primária continua sendo a idempotência: só os realmente inseridos contam; os demais são duplicatas descartadas.
3. **Calcula os deltas no domínio**: para cada (conta, dia) um `SaldoDiario` zerado recebe `Aplicar(movimento)` — a regra de como um
   movimento afeta o saldo continua em um lugar só (o domínio).
4. **Acumula no banco** com `INSERT ... ON CONFLICT (conta, dia) DO UPDATE SET total = total + excluído`: soma **atômica**, sem ler antes,
   sem *retry* por conflito e sem *lost update*. As linhas são enviadas **ordenadas por (conta, dia)**, o que garante a mesma ordem de
   bloqueio em todos os lotes e elimina *deadlock*.
5. Invalida o cache das (conta, dia) afetadas depois do `commit`.

Falhas transitórias (banco fora) refazem o lote inteiro com *retry* exponencial; como o passo 2 é idempotente, repetir é seguro.
Esgotadas as tentativas, o lote vai para a DLQ.

## Alternativas consideradas
- **Manter concorrência otimista (`xmin`)** — correta, mas com *retry* sob contenção e uma ida ao banco por evento.
- **Lock pessimista por saldo** — serializa e reduz vazão.
- **Agregar em memória e gravar de tempos em tempos (write-behind)** — perde dados em queda do processo; descartado para dinheiro.
- **Falha do lote inteiro por um item inválido** — um evento "veneno" bloquearia todos os vizinhos; a quarentena evita isso.

## Consequências
- (+) Vazão muito maior e previsível; contenção por conta quente deixa de gerar *retry*.
- (+) Resultado idêntico ao ledger: na carga de 30 000 lançamentos (3+3 réplicas), `sum(saldos) = sum(ledger)` exatamente, `outbox = 0`,
  `movimentos = lançamentos`.
- (−) SQL específico de PostgreSQL (`unnest`, `ON CONFLICT`), isolado em `ProjecaoRepository` e atrás de `IProjecaoRepository`.
- (−) A latência de um evento passa a incluir a janela do lote (até 200 ms).
- **Padrão atual:** o modo SQL descrito acima é **opcional** (`Projecao:Modo = Sql`); o padrão é EF com repetição do lote em conflito ([ADR-0019](0019-ef-por-padrao-sql-como-otimizacao-medida.md)). A [evidência](../benchmarks/ef-vs-sql.md): o upsert em lote é de 1,7× a 12× mais rápido (e ~5–7× na virada do dia), e o EF sem tratamento de conflito falhou em ~87 % dos lotes concorrentes contra 0 % do EF com repetição e do SQL. A [demonstração](../demos/upsert-demo.sh) mostra a corrida e a transação "envenenada" do PostgreSQL.
- **Validação:** `ConsolidadoProjecaoTests` (PostgreSQL real) — 30 lotes concorrentes sobre as mesmas 10 contas somam exatamente, sem *deadlock*;
  o mesmo lote entregue 8 vezes em paralelo é somado uma vez; evento inválido vai para quarentena sem bloquear os demais.
