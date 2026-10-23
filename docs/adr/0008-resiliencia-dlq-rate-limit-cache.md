# ADR-0008 — Resiliência: retry, DLQ, circuit breaker, rate limit, cache fail-open

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
O banco exige operar em "instabilidade, alta demanda ou falhas parciais". É preciso definir, **para cada dependência**,
o que acontece quando ela falha ou fica lenta, sem propagar a falha ao cliente.

## Decisão

| Falha / situação | Comportamento | Mecanismo |
|---|---|---|
| RabbitMQ ou Consolidado fora | Lançamentos segue aceitando e respondendo `201`; eventos esperam no outbox (inclusive se a fila ainda não existe: publicação *mandatory*) | Outbox transacional + relay ([ADR-0002](0002-transactional-outbox.md), [ADR-0011](0011-relay-de-outbox-proprio.md)) |
| Erro transitório ao projetar (banco fora, timeout) | Reprocessa o lote (idempotente) | `UseMessageRetry` **exponencial** (6 tentativas, 200 ms → 30 s) |
| Evento inválido ("veneno") | Não bloqueia o lote: vai para a **quarentena** `eventos_rejeitados` (payload + motivo) e os demais seguem | Validação por item no handler em lote ([ADR-0012](0012-projecao-em-lote-e-acumulacao-atomica.md)) |
| Tentativas esgotadas | Mensagem preservada na **DLQ** `consolidado.lancamento-criado_error` com a exceção original | MassTransit error queue (replay via RabbitMQ UI/shovel — ver runbook no README) |
| Banco do Consolidado fora por minutos | Para de consumir e evita tempestade de falhas; mensagens ficam no broker | `UseCircuitBreaker` + backpressure (`PrefetchCount=2000`, lotes de 500, 4 lotes simultâneos) |
| Redis lento/fora | Consulta vai ao banco; **cache nunca derruba a leitura** | `FailOpenCache` + decorator + Polly (timeout 250 ms + circuit breaker) — *fail-open* |
| Pico de requisições / abuso | `429` + `Retry-After`; protege os demais usuários | `RateLimiter` token bucket **por usuário** + limitador **global** |
| Consolidado indisponível (leitura) | `readiness` falha só se o **PostgreSQL** cair (Redis/RabbitMQ não entram) | `/health/live`, `/health/ready`, `/health` |

**Meta do desafio anterior preservada:** o Consolidado absorve 50 req/s com perda ≤ 5 % — leitura com cache + índice cobrindo,
limitador global configurável (`RateLimiting:PermitLimitGlobal`, padrão 500/s) e réplicas stateless atrás de balanceador.

## Alternativas consideradas
- **Retry no cliente apenas** — não protege o processamento assíncrono.
- **Redelivery agendado (delayed message scheduler)** — depende do plugin *delayed exchange* do RabbitMQ; deixado como evolução
  para backoff de minutos/horas antes da DLQ.
- **Rate limit no gateway apenas** — o BFF ([ADR-0014](0014-bff-ponto-unico-de-entrada.md)) aplica os mesmos limites na borda (e um limite por IP em `/auth/*`),
  mas o limite em cada serviço continua como última linha de defesa.

## Consequências
- (+) Cada falha tem comportamento definido e testado (`FailOpenCacheTests`, `CachedSaldoDiarioReadRepositoryTests`, testes de integração, simulação no README).
- (−) A DLQ exige **operação**: alerta em `messages > 0` na fila `_error` e procedimento de reprocessamento (runbook).
- (−) O limite do rate limit é por instância **por padrão**; com `RateLimiting:RedisConnectionString` ele passa a ser exato entre réplicas ([ADR-0018](0018-rate-limit-distribuido.md)).
