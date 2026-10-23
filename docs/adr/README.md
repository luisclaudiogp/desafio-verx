# Architecture Decision Records (ADRs)

Formato [MADR](https://adr.github.io/madr/) simplificado: **Contexto → Decisão → Alternativas → Consequências**.
Um ADR aceito não é editado: se a decisão mudar, cria-se um novo que o **substitui**.

| # | Decisão | Status |
|---|---|---|
| [0001](0001-dois-servicos-bounded-contexts.md) | Dois serviços (bounded contexts) em vez de monolito | Aceito |
| [0002](0002-transactional-outbox.md) | Transactional Outbox para publicar eventos | Aceito (entrega: ADR-0011) |
| [0003](0003-cqrs-handlers-proprios.md) | CQRS com handlers próprios (sem MediatR) | Aceito |
| [0004](0004-consistencia-idempotencia-concorrencia.md) | Consistência: consumidor idempotente + concorrência otimista | Substituído em parte (ADR-0012) |
| [0005](0005-ledger-imutavel-estorno-idempotency-key.md) | Ledger imutável, estorno e `Idempotency-Key` | Aceito |
| [0006](0006-saldo-point-in-time.md) | Saldo em um instante: soma sobre movimentos | Aceito (snapshots: ADR-0016) |
| [0007](0007-seguranca-e-protecao-de-dados.md) | Segurança: JWT, autorização por conta e proteção de dados | Aceito (login: ADR-0014 e ADR-0017) |
| [0008](0008-resiliencia-dlq-rate-limit-cache.md) | Resiliência: retry, DLQ, circuit breaker, rate limit, cache fail-open | Aceito (rate limit: ADR-0018) |
| [0009](0009-observabilidade.md) | Observabilidade com OpenTelemetry | Aceito |
| [0010](0010-documentacao-as-code-c4.md) | Documentação como código (ADR + C4 em Markdown/Mermaid) | Aceito |
| [0011](0011-relay-de-outbox-proprio.md) | Relay de Outbox próprio (`SKIP LOCKED`, lotes, paralelo) | Aceito |
| [0012](0012-projecao-em-lote-e-acumulacao-atomica.md) | Projeção em lote com acumulação atômica | Aceito (modo SQL opcional: ADR-0019) |
| [0013](0013-capacidade-e-dimensionamento.md) | Capacidade medida, dimensionamento e como escalar | Aceito |
| [0014](0014-bff-ponto-unico-de-entrada.md) | BFF como ponto único de entrada (gateway + login) | Aceito |
| [0015](0015-saldo-suficiente-e-hash-da-requisicao.md) | Regra de saldo suficiente e hash da requisição na idempotência | Aceito |
| [0016](0016-snapshots-de-saldo.md) | Snapshots de saldo | Aceito |
| [0017](0017-sessao-do-bff-authorization-code-pkce.md) | Sessão do BFF com Authorization Code + PKCE | Aceito |
| [0018](0018-rate-limit-distribuido.md) | Rate limit distribuído (Redis) | Aceito |
| [0019](0019-ef-por-padrao-sql-como-otimizacao-medida.md) | EF por padrão; SQL específico como otimização opcional e medida | Aceito |
| [0020](0020-qualidade-estatica-sonarqube.md) | Qualidade estática contínua (SonarQube, quality gate e revisão de hotspots) | Aceito |

Novo ADR: copie [`template.md`](template.md).
