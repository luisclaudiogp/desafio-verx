# Benchmark: EF (padrão) × SQL otimizado

Evidência para o [ADR-0019](../adr/0019-ef-por-padrao-sql-como-otimizacao-medida.md). Responde: **o que se ganha, e o que se paga, ao
trocar o EF Core pelo SQL específico do PostgreSQL** nos dois pontos de escrita mais disputados do sistema.

## O que é comparado
Cada ponto tem dois modos, escolhidos por configuração (o padrão é EF):

| Ponto | Modo EF (padrão) | Modo SQL (opcional) | Configuração |
|---|---|---|---|
| **Projeção** do Consolidado | `AddRange`/`SaveChanges` + `ExecuteUpdate` por delta; conflito de chave ⇒ **repete o lote** (até 3 vezes) | `INSERT ... ON CONFLICT` com `unnest` (1 comando para todos os deltas) | `Projecao:Modo` = `Ef` \| `Sql` |
| **Posição** de saldo do Lançamentos | **concorrência otimista** (`xmin`); conflito ⇒ repete a operação (até 5 vezes, com espera) | `SELECT ... FOR UPDATE` (trava pessimista) | `Posicao:Modo` = `Ef` \| `Sql` |

Há ainda uma terceira estratégia, **"EF ingênuo"** (sem tratamento de conflito), usada **só como referência** do que aconteceria sem a repetição.
Os dois modos de produção são exercitados pelo **handler real**, não por réplicas.

## Como reproduzir
```bash
docker compose up -d postgres
dotnet run --project src/Verx.Benchmarks -c Release -- --repeticoes 30                    # tudo
dotnet run --project src/Verx.Benchmarks -c Release -- --secao projecao|concorrencia|posicao
docker compose -f docker-compose.yml -f docker-compose.sql.yml up -d --build              # sobe a stack no modo SQL
```
O projeto cria um banco temporário por experimento, migra, aquece, mede e remove o banco. Código em [`src/Verx.Benchmarks`](../../src/Verx.Benchmarks).

## Método
Banco **pré-aquecido** (veja "Uma armadilha de método"), estratégias **intercaladas** a cada repetição, 5 repetições de aquecimento +
30 medidas por cenário, mediana e p95, e **duas execuções completas** (os intervalos abaixo vão da menor à maior). Ambiente: notebook de
desenvolvimento, 24 CPUs, .NET 10, PostgreSQL 16 no Docker Desktop, tudo no mesmo host.

## 1) Projeção: tempo para aplicar um lote (mediana, ms; menor é melhor)

| Cenário | SQL (upsert em lote) | EF (padrão) | EF ÷ SQL |
|---|---:|---:|---:|
| 500 eventos, 10 contas | 14–18 | 27–31 | **1,7× a 2,0×** |
| 500 eventos, 500 contas | 107–111 | 211–291 | **2,0× a 2,6×** |
| 500 eventos, 500 contas **novas** (criam saldos) | 102–112 | 500–767 | **4,9× a 6,9×** |
| 50 eventos, 50 contas (lote pequeno) | 4,8–7,0 | 60–72 | **10× a 12×** |
| 500 eventos, 10 contas, 50 % reenviados | 10–13 | 56–61 | **4,8× a 5,5×** |

Sem conflito, o EF com tratamento custa o **mesmo** que o ingênuo (27 contra 27 ms no primeiro cenário): a repetição não tem custo quando não há conflito.
O cenário de **contas novas** é o mais caro do EF porque cada saldo faltante é inserido na ordem das chaves (um `SaveChanges` por saldo) para
evitar *deadlock*; o ingênuo agrupa as inserções e é mais rápido (177–277 ms), mas falha sob concorrência (próxima tabela).

### Concorrência: 8 trabalhadores aplicando lotes ao mesmo tempo sobre as mesmas contas novas (320 lotes)
É o que acontece na virada do dia UTC, quando todo evento de uma conta cria a linha do dia.

| Estratégia | Falhas definitivas | Lotes repetidos | Saldos = movimentos? |
|---|---:|---:|---|
| SQL (upsert em lote) | **0** | 0 | sim |
| **EF com tratamento (padrão)** | **0** | 277 (≈ 87 % dos lotes precisaram repetir) | sim |
| EF ingênuo (sem tratamento) | **280** (≈ 87 %) | — | sim (os lotes que falham são desfeitos) |

## 2) Posição de saldo: 2.000 registros, 32 em paralelo

| Contas | Modo | Ops/s | p50 (ms) | p95 (ms) | Respostas `409` | Posição = ledger? |
|---:|---|---:|---:|---:|---:|---|
| 200 | EF (padrão) | 1.325–1.515 | 17–20 | 41–46 | 0 | sim |
| 200 | SQL | 1.537–1.700 | 16–19 | 27–31 | 0 | sim |
| 10 | EF (padrão) | 612–724 | 31–36 | 103–125 | **274 (13,7 %)** | sim |
| 10 | SQL | 1.325–1.363 | 17 | 63–70 | 0 | sim |
| 1 | EF (padrão) | 289–294 | 124 | 158–162 | **1.442–1.453 (≈ 72 %)** | sim |
| 1 | SQL | 338–361 | 9–10 | 203–248 | 0 | sim |

Em **nenhum** caso o saldo ficou negativo ou divergiu do ledger: a concorrência otimista **nunca aceita um débito sem saldo**, ela apenas devolve `409`
quando esgota as tentativas (o cliente repete com a mesma `Idempotency-Key`).

## 3) Carga real (k6, 800 req/s de escrita, 200 contas)

| Modo | p95 | Erros |
|---|---:|---:|
| EF (padrão), 4 rodadas | 14–23 ms | 0 % |
| SQL, 2 rodadas | 12–20 ms | 0 % |
| **Conta quente** (50 % das requisições em uma conta, 400 req/s): EF | 47 ms | **5,9 % de `409`** |
| **Conta quente**: SQL | 34 ms | 0 % |

Com 800 req/s espalhados, `outbox = 0`, `lançamentos = movimentos`, `Σ saldos = Σ ledger` e `posição = ledger`, nos dois modos.

## Conclusões
1. **O padrão EF é correto em todos os cenários** (0 falhas definitivas, saldos exatos), e **equivalente ao SQL com carga espalhada** (800 req/s, p95 na mesma faixa).
2. **O SQL é mais rápido na projeção** de 1,7× a 12× (cresce com o número de deltas distintos por lote, com lotes pequenos e com reenvios),
   porque faz **uma ida ao banco** em vez de uma por delta. O servidor insere 500 linhas em ~7,5 ms (`EXPLAIN ANALYZE`); o resto do tempo do EF é ida e volta.
3. **Onde o EF paga de verdade é a conta quente** (muitas escritas simultâneas na mesma conta): a concorrência otimista gera repetições e, ao esgotá-las, `409`.
   Aí o `FOR UPDATE` serializa com custo previsível. **Regra prática:** padrão EF; trocar `Posicao:Modo` para `Sql` quando houver contas de alta
   contenção; trocar `Projecao:Modo` para `Sql` quando o volume de eventos exigir (ou na virada do dia, quando muitas contas criam saldo).
4. O ganho do SQL **não é o SQL ser mais rápido**, é **número de idas ao banco** e **ausência de conflito**.

## Uma armadilha de método (aprendizado)
Uma medição inicial, sem aquecer o banco, fez o SQL parecer **mais lento** que o EF em um cenário (≈55 ms contra ≈40 ms). Era artefato: logo após um
`CHECKPOINT`, o PostgreSQL grava a **página inteira** no WAL na primeira modificação de cada página, e os primeiros dezenas de lotes ficam ~5× mais lentos
(a mesma operação foi de ≈56 ms nos 10 primeiros lotes para ≈11 ms depois do lote 40). Corrigi com o pré-aquecimento, a intercalação das estratégias e
um controle com a ordem dos cenários invertida.

## Limites
- Máquina de desenvolvimento com Docker Desktop: o que vale são as **razões** medidas de forma intercalada, não os valores absolutos (que variaram até ~50 % entre execuções).
- Mede-se a projeção e o registro isolados, sem consumo da fila, cache nem BFF.
- Não otimizei o modo EF além do idiomático (por exemplo, agrupar as inserções de saldos novos em ordem, hoje feitas uma a uma para evitar *deadlock*).
- Há *outliers* ocasionais (≈50 ms) no p95 do SQL cuja causa não investiguei.
