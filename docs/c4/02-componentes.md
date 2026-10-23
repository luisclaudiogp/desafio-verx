# C4 — Componentes (Nível 3)

## Bff.API (borda: login e gateway)

```mermaid
flowchart TB
    cli(["Cliente"])
    subgraph api["Bff.API"]
        rl["<b>Rate limiter distribuído</b><br/>token bucket no Redis (Lua) · fail-open local<br/>por usuário + global · por IP 20/min (/auth/*)"]
        jwt["<b>JwtBearer (AddVerxSecurity)</b><br/>assinatura · emissor · audiência · expiração"]
        pol["<b>ContaDaRotaAuthorizationHandler</b><br/>conta_id do token × {contaId} da rota → 403"]
        auth["<b>AuthController</b><br/>login · refresh · logout · me"]
        tp["<b>ITokenProvider → KeycloakTokenProvider</b><br/>HttpClient + timeout 3 s · 1 retry · circuit breaker"]
        exc["<b>IdentidadeExceptionHandler</b><br/>credenciais inválidas → 401 · IdP fora → 503"]
        yarp["<b>YARP (MapReverseProxy)</b><br/>rotas e clusters por configuração<br/>health ativo + passivo · timeout 30 s<br/>injeta o Bearer da sessão por cookie"]
        sess["<b>Sessão (Authorization Code + PKCE)</b><br/>/bff/login · /signin-oidc · /bff/logout<br/>cookie opaco → bilhete no Redis · renovação transparente<br/>CSRF: X-CSRF ou Sec-Fetch-Site"]
    end
    kc{{"Keycloak"}}
    lanc["Lancamentos.API"]
    cons["Consolidado.API"]
    cli --> rl
    rl --> auth
    rl --> jwt --> pol --> yarp
    auth --> tp --> kc
    cli -- "navegador" --> sess
    sess -- "code + PKCE (PAR)" --> kc
    sess --> jwt
    redis[("Redis<br/>sessões · chaves DP · rate limit")]
    sess --> redis
    rl --> redis
    tp -. "falha" .-> exc
    yarp -- "/lancamentos/**" --> lanc
    yarp -- "/saldo · /saldo-diario/**" --> cons
    classDef c fill:#85bbf0,color:#000,stroke:#5d82a8
    classDef x fill:#999,color:#fff,stroke:#666
    class rl,jwt,pol,auth,tp,exc,yarp,sess c
    class kc,lanc,cons,redis x
```

O contrato de rotas é guardado por `GatewayRotasTests` (cada endpoint dos serviços tem rota no BFF e vice-versa) e o BFF não pode
depender de `Lancamentos`, `Consolidado`, EF Core nem MassTransit (`BffArchitectureTests`).

## Lancamentos.API (lado de escrita)

```mermaid
flowchart TB
    subgraph api["Lancamentos.API"]
        filtro["<b>ContaAuthorizationFilter</b><br/>[AutorizaConta] · 403 se a conta não é do token"]
        ctrl["<b>LancamentosController</b><br/>lê Idempotency-Key e delega"]
        reg["<b>RegistrarLancamentoHandler</b><br/>Command"]
        est["<b>EstornarLancamentoHandler</b><br/>Command (1 estorno por lançamento)"]
        qry["<b>ObterLancamentoHandler</b><br/>Query"]
        regtr["<b>LancamentoPersistidor</b><br/>1 transação: posição + lançamento + evento<br/>repete em conflito otimista (até 5x)"]
        replay["<b>VerificadorDeReplay</b><br/>chave + hash do corpo → replay (200) ou 422"]
        dom["<b>Lancamento</b> (Domain)<br/>imutável · invariantes · Estornar() · HashDaRequisicao"]
        pos["<b>PosicaoConta</b> (Domain)<br/>Aplicar(): débito &gt; saldo → SaldoInsuficiente<br/>EfPosicaoContaRepository (xmin) · modo SQL: FOR UPDATE"]
        repo["<b>LancamentoRepository + EfUnitOfWork</b><br/>unicidade → ChaveDuplicadaException"]
        pub["<b>OutboxLancamentoEventPublisher</b><br/>Lancamento → evento → outbox_eventos<br/>(mesma transação do lançamento)"]
        relay["<b>OutboxRelay + OutboxProcessor</b><br/>N workers · lote · FOR UPDATE SKIP LOCKED<br/>publica em paralelo e apaga na mesma transação"]
        brk["<b>MassTransitBrokerPublisher</b><br/>PublishBatch · mandatory (sem rota = falha)"]
    end
    db[("lancamentos_db<br/>lancamentos · outbox_eventos")]
    mq{{"RabbitMQ"}}
    filtro --> ctrl
    ctrl --> reg & est & qry
    reg --> dom
    est --> dom
    reg --> replay
    est --> replay
    reg --> regtr
    est --> regtr
    regtr -- "corrida na chave" --> replay
    replay --> repo
    est --> repo
    qry --> repo
    regtr --> repo
    regtr --> pos
    regtr --> pub
    repo --> db
    pub --> db
    relay -- "lê e apaga lotes" --> db
    relay --> brk -- "AMQP (confirmação)" --> mq
    classDef c fill:#85bbf0,color:#000,stroke:#5d82a8
    classDef d fill:#2a8f5a,color:#fff,stroke:#1b6a42
    classDef q fill:#c77d12,color:#fff,stroke:#8f5a0a
    pos --> db
    class filtro,ctrl,reg,est,qry,regtr,replay,dom,pos,repo,pub,relay,brk c
    class db d
    class mq q
```

## Consolidado.API (lado de leitura / projeção)

```mermaid
flowchart TB
    mq{{"RabbitMQ"}}
    subgraph api["Consolidado.API"]
        filtro["<b>ContaAuthorizationFilter</b><br/>[AutorizaConta]"]
        cons["<b>LancamentoCriadoLoteConsumer</b><br/>Batch: até 500 msgs ou 200 ms · 4 lotes simultâneos<br/>endpoint: retry exponencial · circuit breaker · DLQ"]
        proj["<b>RegistrarMovimentosEmLoteHandler</b><br/>Command: valida item a item · quarentena dos inválidos<br/>deltas no domínio · repete o lote em conflito (até 3x)"]
        dom["<b>Movimento / SaldoDiario</b> (Domain)<br/>Movimento.Criar() · SaldoDiario.Aplicar()"]
        repoP["<b>ProjecaoRepository</b><br/>padrão: EfProjecaoTransacao (ExecuteUpdate, ordenado)<br/>modo SQL: upsert em lote (unnest, ON CONFLICT)"]
        corr["<b>CorretorDeSnapshots</b><br/>soma o delta nos snapshots posteriores<br/>lock compartilhado · mesma transação"]
        ctrl["<b>SaldosController</b><br/>saldo-diario/{data} · saldo?em="]
        qd["<b>ObterSaldoDiarioHandler</b><br/>Query"]
        qe["<b>ObterSaldoEmHandler</b><br/>Query"]
        cached["<b>CachedSaldoDiarioReadRepository</b><br/>Decorator · cache-aside"]
        inval["<b>SaldoCacheInvalidator</b>"]
        fo["<b>FailOpenCache</b><br/>Polly: timeout + circuit breaker<br/>falha do Redis = sem cache"]
        read["<b>SaldoReadRepository</b><br/>saldo em T = snapshot + dias soltos + movimentos do dia<br/>(LINQ, 3 consultas)"]
        snapw["<b>SnapshotWorker → GerarSnapshotsHandler</b><br/>gera o acumulado dos dias encerrados, em lotes<br/>advisory lock exclusivo por lote"]
        snapr["<b>SnapshotRepository</b>"]
    end
    db[("consolidado_db<br/>movimentos · saldos_diarios<br/>saldo_snapshots · eventos_rejeitados")]
    redis[("Redis")]
    mq --> cons --> proj
    proj --> dom
    proj --> repoP -- "1 transação por lote" --> db
    proj --> corr --> db
    proj -- "após o commit" --> inval --> fo
    filtro --> ctrl
    ctrl --> qd --> cached
    ctrl --> qe --> read
    cached --> fo
    cached -- "miss" --> read
    read --> db
    snapw --> snapr --> db
    fo <-- "get / set / remove" --> redis
    classDef c fill:#85bbf0,color:#000,stroke:#5d82a8
    classDef d fill:#2a8f5a,color:#fff,stroke:#1b6a42
    classDef qq fill:#c77d12,color:#fff,stroke:#8f5a0a
    class filtro,cons,proj,dom,repoP,corr,ctrl,qd,qe,cached,inval,fo,read,snapw,snapr c
    class db,redis d
    class mq qq
```

## Regra de dependência (verificada em CI)

```
API ──► Application ──► Domain ◄── (nada depende de Infrastructure, exceto a API para compor o DI)
            ▲
Infrastructure ┘   (implementa as portas definidas em Application/Domain)
```

Os testes em `src/Verx.ArchitectureTests` (NetArchTest) quebram o build se `Domain`/`Application`
passarem a depender de EF Core, MassTransit, ASP.NET, Redis ou dos contratos de eventos (`Shared.Contracts`),
ou se um contexto delimitado (`Lancamentos` ↔ `Consolidado`) referenciar o outro. O mapeamento
entidade → evento de integração acontece só na `Infrastructure`.
