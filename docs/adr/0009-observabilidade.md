# ADR-0009 — Observabilidade com OpenTelemetry

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
Um fluxo assíncrono (API → Outbox → RabbitMQ → consumidor → banco) é difícil de depurar sem correlação entre as pontas.

## Decisão
**OpenTelemetry** nos dois serviços (extensão única `AddVerxObservability`, evitando divergência):
- **Traces** → OTLP → Jaeger. Instrumenta ASP.NET Core, HttpClient, EF Core e MassTransit (o `traceparent` atravessa o broker,
  então o span do consumidor aparece sob a requisição que originou o lançamento).
- **Métricas** → endpoint `/metrics` (Prometheus): ASP.NET Core, runtime .NET e métricas do MassTransit (mensagens consumidas/falhas).
- **Health checks** separados em *liveness*, *readiness* e diagnóstico completo (ADR-0008).

## SLIs/SLOs propostos (a serem calibrados com tráfego real)
| SLI | SLO |
|---|---|
| Disponibilidade do `POST /lancamentos` (não-5xx) | 99,9 % / 30 dias |
| Latência p95 de `POST /lancamentos` | < 300 ms |
| Latência p95 de `GET /saldo*` | < 150 ms (cache) / < 400 ms (miss) |
| Atraso de projeção (Outbox → saldo) p99 | < 5 s |
| Mensagens na DLQ | 0 (qualquer valor > 0 dispara o alerta `MensagensNaDLQ`, já configurado em `prometheus-alerts.yml`) |

## Alternativas consideradas
- **Logs apenas** — sem correlação entre serviços.
- **APM proprietário** — bom, mas amarra a fornecedor; OTLP mantém portabilidade.

## Consequências
- (+) Diagnóstico ponta a ponta; base para alertas por SLO.
- (−) Amostragem e retenção de traces precisam ser definidas em produção (custo).
