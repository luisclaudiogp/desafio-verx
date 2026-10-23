# ADR-0011 — Relay de Outbox próprio (`SKIP LOCKED`, em lotes e em paralelo)

- **Status:** Aceito — substitui a parte de *entrega* do [ADR-0002](0002-transactional-outbox.md)
- **Data:** 2026-10-19

## Contexto
O padrão Transactional Outbox do ADR-0002 foi mantido, mas a **entrega** feita pelo Bus Outbox do MassTransit não escalou.
Medição (laptop, mesma carga, rate limit liberado):

| Cenário | Escrita (req/s aceitos) | Backlog do outbox após 30 s | Vazão de entrega ao broker |
|---|---|---|---|
| Bus Outbox padrão (lote 100) | ~990 | ~30 000 e crescendo | **~150 eventos/s** |
| Bus Outbox com lotes de 2 000 | ~890, p95 de escrita **1,3 s** | ~24 000 e crescendo | piorou (~40 eventos/s) |
| **Relay próprio (este ADR)** | ~920 | **0** | acompanha a escrita em tempo real |

Causa: o delivery service do MassTransit trabalha com **um bloqueio de linha** (`OutboxState`, isolamento serializável) e publica
em série. Há um único entregador efetivo no cluster inteiro, e os erros `40001` (falha de serialização) apareciam sob carga.
Aumentar o tamanho do lote só aumentou a contenção.

## Decisão
Tabela própria `outbox_eventos` (`Id`, `Payload` JSON, `CriadoEm`), gravada **na mesma transação** do lançamento
(`OutboxLancamentoEventPublisher`, atrás da porta `ILancamentoEventPublisher`). Um `OutboxRelay` (`BackgroundService`) com
`N` workers executa o ciclo, em uma transação por lote (`OutboxProcessor`):

1. `SELECT ... FROM outbox_eventos ORDER BY "CriadoEm" LIMIT n FOR UPDATE SKIP LOCKED` — workers e **réplicas disputam sem se bloquear**;
2. publica o lote inteiro no broker em paralelo (`PublishBatch`, via a porta `IBrokerPublisher`);
3. `DELETE ... WHERE "Id" = ANY(@ids)` e `COMMIT`.

Garantias:
- **At-least-once:** se o processo cair entre publicar e confirmar, o lote volta e é republicado; o consumidor é idempotente ([ADR-0012](0012-projecao-em-lote-e-acumulacao-atomica.md)).
- **Sem perda por falta de rota:** a publicação é *mandatory*. Se a fila do consumidor ainda não existe (primeiro boot, ordem de subida),
  o RabbitMQ devolveria a mensagem descartada e o relay apagaria o evento. Com *mandatory* a publicação falha, o lote faz *rollback* e o
  evento permanece no outbox (testado: ver "Validação").
- **Backoff exponencial** (até 5 s) quando o broker está fora; as métricas `verx_outbox_publicados_total`, `verx_outbox_falhas_total`
  e `verx_outbox_tamanho_lote` ficam no Prometheus (`Meter` `Verx.Outbox`).
- A ordem entre contas não é garantida (e não precisa ser: a projeção é comutativa).

Parâmetros em `Outbox:*` (`BatchSize` 500, `Paralelismo` 2, `PollOciosoMs` 50, `BackoffMaximoMs` 5000).

## Alternativas consideradas
- **Ajustar o Bus Outbox do MassTransit** — medido acima; não resolve o bloqueio único.
- **CDC (Debezium → Kafka)** — a solução mais escalável, porém exige Kafka Connect e mais infraestrutura. Fica como evolução quando um único
  PostgreSQL deixar de bastar.
- **Publicar direto após o commit** — *dual write*; perde evento em falha.
- **Processo separado só para o relay** — vale quando se quiser escalar entrega e API de forma independente; hoje roda dentro da API e
  o `Paralelismo` é configurável (pode ser 0/1 em réplicas que não devam entregar).

## Consequências
- (+) Entrega acompanha a escrita; vários workers e réplicas sem bloqueio; falha do broker não perde nem duplica de forma visível.
- (+) Remove as tabelas e a dependência `MassTransit.EntityFrameworkCore`.
- (−) Código de infraestrutura próprio para manter (compensado por testes de integração com PostgreSQL real).
- (−) `INSERT`/`DELETE` constantes geram *bloat*: depende do autovacuum (monitorar tuplas mortas de `outbox_eventos`). Em escala maior,
  particionar a tabela por tempo e descartar partições.
- **Validação:** `Verx.IntegrationTests.OutboxRelayTests` — 6 workers sobre 2 000 eventos publicam cada um exatamente uma vez; broker
  indisponível preserva os eventos; evento e lançamento somem juntos em *rollback*. E manualmente: com o Consolidado parado o evento ficou
  no outbox e foi entregue quando ele subiu.
