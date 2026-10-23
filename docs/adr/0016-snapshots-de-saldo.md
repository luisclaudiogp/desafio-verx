# ADR-0016 — Snapshots de saldo

- **Status:** Aceito (implementa a evolução prevista no [ADR-0006](0006-saldo-point-in-time.md))
- **Data:** 2026-10-20

## Contexto
`GET /saldo?em=<instante>` somava **todos** os movimentos da conta até o instante: custo linear com a vida da conta. Para contas com
milhões de movimentos isso vira o gargalo de leitura.

## Decisão
Guardar o **acumulado de créditos e débitos ao fim de cada dia encerrado** (`saldo_snapshots`, PK `(ContaId, Data)`) e calcular:

```
saldo(T) = snapshot mais recente com Data < dia(T)
         + saldos_diarios dos dias entre o snapshot e dia(T)      (poucos: só os ainda sem snapshot)
         + movimentos do próprio dia(T) até T                      (poucos: um dia)
```
Uma única consulta SQL (CTE) faz isso. O custo deixa de depender do tamanho do histórico.

**Geração.** `GerarSnapshotsHandler` (Application) calcula a data-limite pela política de domínio `PoliticaDeSnapshot`
(`hoje − MargemDias`, mínimo 1: só dias encerrados) e chama `ISnapshotRepository.GerarLoteAsync` em lotes de contas até não haver
mais nada a fazer. O `SnapshotWorker` repete isso a cada `Snapshots:IntervaloSegundos` (padrão 600). A geração é **idempotente**
(`ON CONFLICT DO NOTHING`) e **incremental**: o acumulado novo é o último snapshot da conta mais a soma dos dias seguintes.

**Eventos atrasados (a parte difícil).** Um movimento com `OcorridoEm` em um dia que já tem snapshot posterior tornaria o snapshot
errado. Por isso a projeção, **na mesma transação** que acumula `saldos_diarios`, soma o delta em todos os snapshots com
`Data >= data do movimento` (`ICorretorDeSnapshots`, uma porta própria fora da transação de projeção; linhas travadas em ordem para evitar deadlock). É um `UPDATE` por lote, sem
efeito no caso comum (evento de hoje).

**Corrida gerador × projeção.** O gerador toma um *advisory lock* **exclusivo** por lote de contas e a projeção, um **compartilhado**
do momento da correção até o *commit* do lote dela. Assim, ou o evento atrasado é confirmado antes do lote do gerador (que então o enxerga), ou depois (e então
corrige o snapshot que o gerador acabou de criar). Nunca o intermediário.

## Alternativas consideradas
- **Saldo corrente (*running balance*) por movimento** — leitura O(1), mas movimentos retroativos obrigam a reescrever a cauda e a
  escrita serializa por conta.
- **View materializada atualizada por *cron*** — atrasa a leitura e recalcula tudo; os eventos atrasados exigiriam *refresh* total.
- **Snapshot só mensal** — menos linhas, mas o trecho entre o snapshot e T cresceria (até um mês de dias/movimentos).
- **Somente `saldos_diarios`** — já ajuda (O(dias)), porém ainda cresce com a idade da conta; os snapshots tornam o trecho limitado.

## Consequências
- (+) Resposta exata e de custo limitado, **igual à soma bruta em qualquer instante**: o teste de integração compara com a soma dos
  movimentos em 84 instantes por conta, com e sem snapshots, após eventos atrasados e com gerador e projeção concorrentes.
- (+) Nenhuma mudança de contrato da API.
- (−) Uma tabela a mais (uma linha por conta e dia ativo) e um processo em segundo plano. Particionar `saldo_snapshots` por data é a evolução.
- (−) O hot path da projeção ganha dois comandos (lock compartilhado + correção); sem impacto mensurável a 800 req/s.
- (−) Dias sem atividade não geram linha: o intervalo entre snapshot e T é coberto por `saldos_diarios`, não por snapshots.
