# ADR-0005 — Ledger imutável, estorno e `Idempotency-Key`

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
Em um banco, o histórico financeiro não pode ser reescrito, e clientes/canais reenviam requisições (timeout, retry de rede).
Uma requisição repetida **não pode** gerar dois débitos.

## Decisão
- **Lançamento é imutável**: não há `UPDATE`/`DELETE`. A entidade `Lancamento` só expõe fábricas (`Registrar`, `Estornar`)
  e getters; o teste de arquitetura `Entidades_NaoExpoemSettersPublicos` garante isso.
- **Correção por estorno**: `Estornar` cria um novo lançamento de **tipo oposto e mesmo valor**, apontando para o original
  (`EstornoDeId`). Um índice único parcial garante **um estorno por lançamento**, mesmo sob concorrência.
- **Idempotência na borda**: `POST` exige o header `Idempotency-Key`. Índice único `(ContaId, ChaveIdempotencia)`.
  Repetição ⇒ `200 OK` + `Idempotent-Replayed: true` e o **mesmo** lançamento; corrida entre duas requisições iguais ⇒ a perdedora
  captura a violação e devolve o vencedor.
- **A chave considera o corpo** e **débitos exigem saldo**: ver [ADR-0015](0015-saldo-suficiente-e-hash-da-requisicao.md) (`HashRequisicao`, `PosicaoConta`).
- **Dinheiro** é o value object `Dinheiro`: positivo, ≤ 2 casas decimais, com teto; `numeric(18,2)` no banco. O sinal vem do `Tipo`.
- **Tempo**: `OcorridoEm` (fato gerador) ≠ `RegistradoEm` (auditoria); relógio via `TimeProvider` (testável); tudo em UTC.
- **Auditoria**: `RegistradoPor` guarda o `sub` do token.

## Alternativas consideradas
- **Deduplicar por (conta, valor, tipo, janela de tempo)** — heurística que gera falsos positivos (dois débitos legítimos iguais).
- **Permitir editar/excluir** — destrói trilha de auditoria; rejeitado.
- **Chave de idempotência gerada pelo servidor** — não resolve o retry do cliente, que não sabe se a primeira chamada vingou.

## Consequências
- (+) Retentável com segurança; trilha de auditoria completa; saldo sempre reconstruível a partir do ledger.
- (−) Chave de idempotência precisa ser retida enquanto houver risco de retry (hoje: indefinidamente, pois é parte do ledger).
  Em escala, mover para uma tabela com TTL ou particionar por data.
- (−) Estorno gera dois eventos no consolidado (original + compensação) — comportamento desejado.
