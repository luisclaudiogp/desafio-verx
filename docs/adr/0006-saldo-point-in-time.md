# ADR-0006 — Saldo em um instante: soma sobre movimentos

- **Status:** Aceito — a evolução com snapshots foi implementada no [ADR-0016](0016-snapshots-de-saldo.md)
- **Data:** 2026-10-19

## Contexto
O requisito é consultar "a posição consolidada de um cliente **em um determinado momento**", e não só o total do dia.

## Decisão
O Consolidado mantém duas estruturas:
- `movimentos` — cópia mínima e imutável de cada lançamento (conta, tipo, valor, `OcorridoEm`).
- `saldos_diarios` — agregado por (conta, dia UTC), atualizado na mesma transação.

Consultas:
- `GET /api/contas/{id}/saldo-diario/{data}` → lê `saldos_diarios` (O(1), **cacheado** no Redis).
- `GET /api/contas/{id}/saldo?em=<instante>` → `SUM(créditos) − SUM(débitos)` de `movimentos` com `OcorridoEm <= em`,
  usando o índice cobrindo `(ContaId, OcorridoEm) INCLUDE (Tipo, Valor)` (varredura apenas do índice).
  Sem `em`, usa "agora". **Não é cacheado** (depende de T e de movimentos tardios).

Dia = UTC. O fuso do negócio (ex.: America/Sao_Paulo) é uma evolução simples: converter na borda e armazenar o dia local.

## Alternativas consideradas
- **Somente saldo diário** — não responde "em um instante" (perde precisão intradiária).
- **Saldo acumulado (running balance) por movimento** — leitura O(1), mas escrita serializa por conta e movimentos
  tardios (backdated) obrigam a recalcular a cauda.
- **Snapshots periódicos + soma incremental** — a evolução natural quando uma conta tiver milhões de movimentos:
  `saldo(T) = snapshot(≤T) + SUM(movimentos entre snapshot e T)`. **Implementado depois**, ver ADR-0016.

## Consequências
- (+) Resposta exata para qualquer instante e correta mesmo com lançamentos retroativos.
- (−) Custo cresce linearmente com o nº de movimentos da conta até o instante; mitigado pelo índice cobrindo e pela evolução com snapshots.
