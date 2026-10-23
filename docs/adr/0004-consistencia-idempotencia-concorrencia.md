# ADR-0004 — Consistência: consumidor idempotente + concorrência otimista

- **Status:** Substituído em parte pelo [ADR-0012](0012-projecao-em-lote-e-acumulacao-atomica.md) (concorrência do consumidor); a idempotência por chave primária permanece
- **Data:** 2026-10-19

## Contexto
Com Outbox a entrega é **at-least-once**: o mesmo evento pode chegar mais de uma vez (retry, redelivery, falha após o
commit e antes do *ack*). Além disso, vários consumidores processam em paralelo e podem atualizar o **mesmo saldo**.
A implementação ingênua (ler saldo → somar em memória → salvar) sofre de:
1. **Duplicidade**: o mesmo lançamento somado duas vezes.
2. **Lost update**: duas atualizações concorrentes, uma sobrescreve a outra.

## Decisão
No `RegistrarMovimentoHandler` (Consolidado), numa **única transação**:

1. **Idempotência** — a tabela `movimentos` tem `LancamentoId` como **chave primária**. Verificação prévia (`ExisteAsync`) +
   a PK como garantia final contra corrida. Evento repetido ⇒ descartado, sem efeito.
2. **Concorrência otimista** — `saldos_diarios` usa a coluna de sistema `xmin` do PostgreSQL como *concurrency token*.
   Conflito ⇒ `ConcorrenciaException` (transitória) ⇒ **retry com backoff** do MassTransit, que relê o saldo atual e reaplica.
3. **Abertura concorrente do saldo** (dois consumidores criando o mesmo dia): a violação de PK é diferenciada de duplicidade
   (`ExisteAsync` de novo): se o movimento não existe, vira `ConcorrenciaException` e é repetida.
4. Cache invalidado **após** o commit.

A soma é **comutativa**, portanto a ordem de chegada dos eventos não importa.

## Alternativas consideradas
- **`UPDATE saldo = saldo + x` (upsert atômico)** — também correto e mais barato sob contenção alta; porém foge do agregado
  de domínio (`SaldoDiario.Aplicar`) e dificulta testar regras. É a otimização que adotaria se a contenção por conta/dia virar gargalo.
- **Lock pessimista (`SELECT ... FOR UPDATE`)** — serializa e reduz vazão; desnecessário com baixa contenção por conta.
- **Inbox do MassTransit** — resolve duplicidade, mas a PK do domínio já resolve com menos peças e sem tabela extra.
- **Exactly-once** — não existe de ponta a ponta; o que se faz é *at-least-once + idempotência*.

## Consequências
- (+) Provado em teste contra PostgreSQL real (Testcontainers): 60 movimentos concorrentes → saldo exato;
  10 entregas simultâneas do mesmo evento → somado uma única vez (`Verx.IntegrationTests`).
- (+) Falhas transitórias não perdem dado: após esgotar retries a mensagem vai à DLQ para reprocessamento ([ADR-0008](0008-resiliencia-dlq-rate-limit-cache.md)).
- (−) Consistência **eventual**: janela típica < 2 s (poll do Outbox + processamento). O contrato da API de saldo deve
  deixar isso explícito para o negócio.
- (−) `xmin` é específico de PostgreSQL (acopla a infraestrutura; encapsulado em `ConsolidadoDbContext`).
