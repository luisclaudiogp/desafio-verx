# ADR-0002 — Transactional Outbox para publicar eventos

- **Status:** Aceito — o padrão permanece; a *entrega* foi substituída pelo [ADR-0011](0011-relay-de-outbox-proprio.md)
- **Data:** 2026-10-19

## Contexto
Gravar o lançamento no banco **e** publicar o evento no broker são duas operações em sistemas diferentes.
Fazê-las em sequência cria o problema do *dual write*: se o processo cair entre as duas, o ledger tem o lançamento
mas o saldo nunca será atualizado (ou o contrário). Em dados financeiros isso é inaceitável.

## Decisão
Gravar o evento numa tabela de outbox (`outbox_eventos`; inicialmente o Bus Outbox do MassTransit, trocado pelo [ADR-0011](0011-relay-de-outbox-proprio.md) após medição)
**na mesma transação** do lançamento; um *relay* o entrega ao RabbitMQ de forma assíncrona e repete até confirmar.
O handler depende apenas da porta `ILancamentoEventPublisher`; a gravação no outbox, o mapeamento entidade → evento e a publicação ficam em `Infrastructure`.

Entrega **at-least-once**; a deduplicação é responsabilidade do consumidor ([ADR-0004](0004-consistencia-idempotencia-concorrencia.md)).

## Alternativas consideradas
- **Publicar direto após `SaveChanges`** — dual write; perde eventos em falha. Rejeitado.
- **2PC/transação distribuída** — sem suporte prático em RabbitMQ; frágil e lento.
- **CDC (Debezium → Kafka)** — excelente em escala, mas exige infraestrutura adicional; fica como evolução (ver README).
- **Event sourcing completo** — poderoso, porém muito acima da complexidade necessária aqui.

## Consequências
- (+) Se o RabbitMQ ou o Consolidado cair, `POST /lancamentos` continua respondendo `201`; os eventos acumulam no Outbox
  e são entregues na recuperação (demonstrado em [README → Resiliência](../../README.md#simulando-falhas)).
- (+) Atende diretamente o requisito "controle de lançamentos não pode ficar indisponível se o consolidado cair".
- (−) Latência extra de ~1 s (intervalo de polling do outbox, `QueryDelay`) até o evento sair.
- (−) Tabelas Outbox crescem; o MassTransit faz a limpeza das mensagens entregues, e a retenção deve ser monitorada.
