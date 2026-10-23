# C4 — Contexto e Containers

Diagramas como código (Mermaid, renderizados pelo GitHub). A mesma modelagem também está em
[`workspace.dsl`](workspace.dsl) (Structurizr DSL) para quem quiser gerar/explorar com o Structurizr Lite.

## Nível 1 — Contexto

```mermaid
flowchart LR
    cliente(["Aplicação cliente / Canal interno<br/><i>Pessoa / sistema</i>"])
    sistema["<b>Verx Finance</b><br/><i>Sistema de software</i><br/>Registra lançamentos e disponibiliza<br/>o saldo consolidado por conta"]
    idp["<b>Keycloak (OIDC)</b><br/><i>Sistema externo</i><br/>Emite JWT com a claim conta_id"]
    cliente -- "lança e consulta saldo, faz login<br/>HTTPS + Bearer JWT (único endereço)" --> sistema
    sistema -- "login (client confidencial)<br/>e validação de assinatura (JWKS)" --> idp
    classDef pessoa fill:#08427b,color:#fff,stroke:#052e56
    classDef sis fill:#1168bd,color:#fff,stroke:#0b4884
    classDef ext fill:#999,color:#fff,stroke:#666
    class cliente pessoa
    class sistema sis
    class idp ext
```

## Nível 2 — Containers

```mermaid
flowchart LR
    cliente(["Aplicação cliente / Canal"])
    idp["Keycloak<br/><i>OIDC / JWT</i>"]
    subgraph sis["Verx Finance"]
        direction LR
        bff["<b>Bff.API</b><br/>ASP.NET Core 10 + YARP<br/>login (PKCE + sessão) · valida JWT e conta<br/>rate limit distribuído · roteamento<br/><i>único publicado</i>"]
        subgraph esc["Escrita (rede interna)"]
            lanc["<b>Lancamentos.API</b><br/>ASP.NET Core 10<br/>registrar / estornar<br/>idempotente"]
            dbl[("lancamentos_db<br/>PostgreSQL<br/>ledger + posição por conta + outbox")]
        end
        mq{{"<b>RabbitMQ</b><br/>fila consolidado.lancamento-criado<br/>+ DLQ (_error)"}}
        subgraph leit["Leitura / Projeção (rede interna)"]
            cons["<b>Consolidado.API</b><br/>ASP.NET Core 10<br/>consumidor idempotente<br/>+ consultas de saldo"]
            dbc[("consolidado_db<br/>PostgreSQL<br/>movimentos + saldo diário + snapshots")]
            redis[("Redis<br/>cache do saldo · sessões do BFF<br/>rate limit distribuído")]
        end
    end
    obs["Jaeger / Prometheus<br/><i>OpenTelemetry</i>"]
    cliente -- "/auth/login · /api/contas/**" --> bff
    bff -- "login / refresh / logout<br/>(client confidencial) · JWKS" --> idp
    bff -- "/lancamentos/**" --> lanc
    bff -- "/saldo · /saldo-diario/**" --> cons
    lanc -- "lançamento + outbox<br/>(1 transação)" --> dbl
    dbl -. "entrega assíncrona<br/>(relay do Outbox)" .-> mq
    mq -- "LancamentoCriadoEvent" --> cons
    cons -- "movimento + saldo<br/>(1 transação)" --> dbc
    cons <-- "lê / invalida" --> redis
    bff <-- "sessões · rate limit" --> redis
    bff -.-> obs
    lanc -.-> obs
    cons -.-> obs
    classDef svc fill:#1168bd,color:#fff,stroke:#0b4884
    classDef edge fill:#0b4884,color:#fff,stroke:#052e56
    classDef db fill:#2a8f5a,color:#fff,stroke:#1b6a42
    classDef q fill:#c77d12,color:#fff,stroke:#8f5a0a
    classDef ext fill:#999,color:#fff,stroke:#666
    class lanc,cons svc
    class bff edge
    class dbl,dbc,redis db
    class mq q
    class idp,obs ext
```

**Decisões-chave**
- `Bff.API` é o **único container publicado** ao cliente ([ADR-0014](../adr/0014-bff-ponto-unico-de-entrada.md)): faz o login no Keycloak
  (o segredo do client fica no servidor), valida o JWT e a posse da conta e roteia para os serviços internos. Não tem regra de negócio.
- `Lancamentos.API` **não conhece** o `Consolidado.API` (nenhuma chamada síncrona entre eles). O único acoplamento é o contrato do
  evento em `Shared.Contracts`. Por isso a queda do consolidado, do RabbitMQ ou do Redis não derruba o registro de lançamentos
  (ver [ADR-0002](../adr/0002-transactional-outbox.md)).
