# ADR-0013 — Capacidade, dimensionamento e como escalar

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
O requisito é suportar **milhões de transações**. Antes de otimizar, a solução foi medida sob carga (k6, scripts em [`load/`](../../load)),
e cada gargalo encontrado virou uma decisão (ADR-0011 e ADR-0012).

## Medições
Ambiente: **um único notebook** (24 vCPU, Docker Desktop), com API, PostgreSQL, RabbitMQ, Redis, Keycloak **e o gerador de carga k6 no mesmo
host**. São números de referência e de comparação, **não** de produção. O usuário de carga tem 200 contas, e o token carrega as 200 claims
`conta_id` (validar e analisar esse token custa CPU a mais por requisição do que um usuário real com poucas contas).

| Teste | Resultado |
|---|---|
| Escrita, 1 réplica, **800 req/s** | p95 **14 ms**, 0 % de erro; projeção sem atraso (outbox = 0) |
| Escrita, 1 réplica, ~1 000 req/s | satura o host: p95 acima de 300 ms |
| Escrita, **3 réplicas** de cada serviço | ~1 400 req/s aceitos (host compartilhado já saturado); **consistência exata** |
| Leitura (saldo diário em cache + saldo em instante), **2 500 req/s** | p95 **7,8 ms**, 0 % de erro |
| **Via BFF** (1 instância), escrita **800 req/s** | p95 **22 ms** (+8 ms do salto extra), 0 % de erro, projeção sem atraso |
| Escrita direta **800 req/s** (200 contas), saldo forte ([ADR-0015](0015-saldo-suficiente-e-hash-da-requisicao.md)): EF (padrão) · SQL | p95 **14–23 ms** · **12–20 ms**, 0 % de erro; `posições = soma do ledger`, nenhum saldo negativo, lançamentos = movimentos |
| Escrita com **conta quente** (200 req/s em uma conta): EF · SQL | **5,9 % de `409`** (p95 47 ms) · 0 % (p95 34 ms); ver [ADR-0019](0019-ef-por-padrao-sql-como-otimizacao-medida.md) |
| Via BFF **com rate limit distribuído e sessão** (a quente) | escrita 800 req/s p95 **20 ms**; leitura 1.000 / 1.500 req/s p95 **5,7 ms / 8 ms**; 0 % de erro |
| **Via BFF** (1 instância), leitura 1 000 / 1 500 req/s | p95 **12 ms** / **20 ms**, 0 % de erro |
| **Via BFF** (1 instância), leitura 2 500 req/s | **satura** (≈1 000 req/s efetivos, p95 de segundos): o BFF repete a validação do JWT de 200 claims e proxia cada requisição |

O BFF ([ADR-0014](0014-bff-ponto-unico-de-entrada.md)) é *stateless*: o limite acima é de **uma instância** num host compartilhado e se
resolve com réplicas atrás de um balanceador (não medido aqui). Os scripts de carga atacam os serviços pela rede interna do Docker; para medir
pela borda, passe `-e LANC=http://bff:8080` e `-e CONS=http://bff:8080`.

Conversão: 800 req/s sustentados equivalem a ~69 milhões de lançamentos/dia em um único notebook. **5 milhões/dia ≈ 58 req/s de média**,
com picos de dezenas de vezes isso, que cabem folgados.

O que a 1ª medição revelou (e foi corrigido): entrega do outbox limitada a ~150 eventos/s, projeção de um evento por vez, e pools de conexão
somando mais do que o `max_connections` do PostgreSQL (`too many clients`).

## Decisões de dimensionamento
- **Orçamento de conexões:** `réplicas × Maximum Pool Size` (+ conexões do relay) **≤** `max_connections` do PostgreSQL. O compose usa pool 50 e
  `max_connections` 200 no override de carga. Para muitas réplicas, usar PgBouncer (modo *transaction*).
- **Rate limit** por usuário e global: protege contra abuso sem derrubar os demais (valores em `RateLimiting:*`); no BFF é distribuído no Redis ([ADR-0018](0018-rate-limit-distribuido.md)).
- **Saldo forte na escrita:** o padrão é concorrência otimista (EF) e custa o mesmo que a trava com carga espalhada; em **contas muito disputadas** gera `409` e a saída é `Posicao:Modo = Sql` ([ADR-0019](0019-ef-por-padrao-sql-como-otimizacao-medida.md)).
- **Backpressure** no consumo: `PrefetchCount` ≥ `LoteTamanho × LoteConcorrencia` (validado por teste), lotes de 500, circuit breaker.
- **Autovacuum** atento às tabelas de alto *churn* (`outbox_eventos`).

## Como escalar além do medido (ordem de custo-benefício)
1. **Réplicas** de `Lancamentos.API` e `Consolidado.API` (já provado correto: o relay usa `SKIP LOCKED` e a projeção é idempotente e atômica).
2. **Banco do ledger:** ajustes de PostgreSQL (WAL, `shared_buffers`, discos NVMe), réplica de leitura, PgBouncer.
3. ~~**Saldo em um instante** cresce com o nº de movimentos da conta~~ — **feito**: snapshots de saldo ([ADR-0016](0016-snapshots-de-saldo.md)).
4. **Particionar** `lancamentos`, `movimentos` e `outbox_eventos` por data; arquivar partições antigas.
5. **Shard por conta** (hash do `ContaId`): cada fatia com seu PostgreSQL; o `ContaId` já está em todas as chaves e rotas.
6. **Kafka + CDC** no lugar do relay quando um único PostgreSQL de escrita deixar de ser suficiente; filas *quorum* no RabbitMQ para alta disponibilidade.
7. **PostgreSQL com HA** (Patroni/serviço gerenciado), RPO de segundos, e réplicas do RabbitMQ em 3 nós.

## Como reproduzir
```bash
docker compose -f docker-compose.yml -f docker-compose.load.yml up -d --build
docker run --rm --network verx_verx_net -v "$PWD/load:/scripts" grafana/k6 run /scripts/escrita.js -e RATE=800 -e DURACAO=30s
docker run --rm --network verx_verx_net -v "$PWD/load:/scripts" grafana/k6 run /scripts/leitura.js -e RATE=2500 -e DURACAO=30s
# pela borda (BFF): acrescente -e LANC=http://bff:8080 (escrita) ou -e CONS=http://bff:8080 (leitura)
# várias réplicas: acrescente -f docker-compose.scale.yml e --scale lancamentos-api=3 --scale consolidado-api=3
```
`docker-compose.load.yml` libera o rate limit e ajusta o PostgreSQL **somente para teste**. O `escrita.js` credita 1.000.000 em cada conta no `setup()`, porque débitos agora exigem saldo.

## Consequências
- (+) Capacidade e gargalos conhecidos com evidência; cada limite tem a alavanca de escala correspondente.
- (−) Não há teste de carga no CI e os números dependem do host; antes de produção, repetir em ambiente isolado, com a carga em máquina separada
  e com perfil de contas realista.
