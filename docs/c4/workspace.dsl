workspace "Verx Finance" "Movimentacoes e saldo consolidado de um banco digital" {

    model {
        cliente = person "Cliente / Canal interno" "Registra movimentacoes e consulta a posicao da propria conta"
        idp = softwareSystem "Keycloak" "OIDC / JWT" "External"

        sistema = softwareSystem "Verx Finance" "Ledger de lancamentos e saldo consolidado" {
            bff = container "Bff.API" "Unico ponto de entrada: login no Keycloak, validacao de JWT/conta, rate limit e roteamento (YARP)" "ASP.NET Core 10"
            lancamentos = container "Lancamentos.API" "Comandos (registrar/estornar). Idempotente (chave + hash do corpo). Valida saldo suficiente. Ledger + posicao + Outbox na mesma transacao" "ASP.NET Core 10"
            dbLanc = container "lancamentos_db" "Ledger imutavel + Outbox" "PostgreSQL" "Database"
            broker = container "RabbitMQ" "Fila consolidado.lancamento-criado + DLQ" "MassTransit" "Queue"
            consolidado = container "Consolidado.API" "Projecao idempotente + consultas de saldo (CQRS read side) + snapshots de saldo" "ASP.NET Core 10"
            dbCons = container "consolidado_db" "Movimentos projetados + saldo diario" "PostgreSQL" "Database"
            cache = container "Redis" "Cache-aside do saldo diario (fail-open), sessoes do BFF e rate limit distribuido" "Redis" "Database"
        }

        cliente -> bff "Login, lancamentos e consulta de saldo" "HTTPS/JSON"
        bff -> idp "Login / refresh / logout (client confidencial) e JWKS" "HTTPS"
        bff -> lancamentos "Roteia /lancamentos" "HTTP/JSON"
        bff -> consolidado "Roteia /saldo e /saldo-diario" "HTTP/JSON"
        lancamentos -> dbLanc "Lancamento + outbox (1 transacao)" "SQL"
        lancamentos -> broker "Entrega assincrona do Outbox" "AMQP"
        broker -> consolidado "LancamentoCriadoEvent" "AMQP"
        consolidado -> dbCons "Movimento + saldo (1 transacao)" "SQL"
        consolidado -> cache "Le / invalida" "RESP"
        bff -> cache "Sessoes (PKCE), chaves Data Protection e rate limit" "RESP"
        lancamentos -> idp "JWKS" "HTTPS"
        consolidado -> idp "JWKS" "HTTPS"
    }

    views {
        systemContext sistema "Contexto" { include * autolayout lr }
        container sistema "Containers" { include * autolayout lr }

        styles {
            element "Person" { shape Person background #08427b color #ffffff }
            element "External" { background #999999 color #ffffff }
            element "Database" { shape Cylinder }
            element "Queue" { shape Pipe }
        }
    }
}
