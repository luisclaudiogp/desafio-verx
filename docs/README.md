# Documentação — Verx Finance

Toda a documentação é **doc-as-code**: Markdown, Mermaid e Structurizr DSL versionados junto com o código ([ADR-0010](adr/0010-documentacao-as-code-c4.md)).

## Por onde começar
1. [`../README.md`](../README.md): visão geral, como rodar, API, testes e trade-offs.
2. [`arquitetura.md`](arquitetura.md): fluxos, modos de falha, requisitos não funcionais e ameaças.
3. [`c4/`](c4/): os diagramas, do contexto aos componentes.
4. [`adr/`](adr/README.md): o porquê de cada decisão, com alternativas e consequências.

## Diagramas (C4 Model)
| Documento | Conteúdo |
|---|---|
| [`c4/01-contexto-e-containers.md`](c4/01-contexto-e-containers.md) | Nível 1 (contexto) e nível 2 (containers) |
| [`c4/02-componentes.md`](c4/02-componentes.md) | Nível 3 (componentes) do BFF, de Lançamentos e de Consolidado |
| [`c4/workspace.dsl`](c4/workspace.dsl) | O mesmo modelo em Structurizr DSL |

## Decisões arquiteturais (ADRs)
Índice completo, com status de cada uma: [`adr/README.md`](adr/README.md). Para escrever uma nova, copie o [`template`](adr/template.md).

| Tema | ADRs |
|---|---|
| Estrutura e padrões | [0001 Bounded contexts](adr/0001-dois-servicos-bounded-contexts.md) · [0003 CQRS](adr/0003-cqrs-handlers-proprios.md) · [0010 Doc-as-code e C4](adr/0010-documentacao-as-code-c4.md) |
| Consistência e mensageria | [0002 Outbox](adr/0002-transactional-outbox.md) · [0004 Idempotência e concorrência](adr/0004-consistencia-idempotencia-concorrencia.md) · [0011 Relay de outbox](adr/0011-relay-de-outbox-proprio.md) · [0012 Projeção em lote](adr/0012-projecao-em-lote-e-acumulacao-atomica.md) |
| Domínio | [0005 Ledger imutável e estorno](adr/0005-ledger-imutavel-estorno-idempotency-key.md) · [0006 Saldo no tempo](adr/0006-saldo-point-in-time.md) · [0015 Saldo suficiente e hash](adr/0015-saldo-suficiente-e-hash-da-requisicao.md) · [0016 Snapshots](adr/0016-snapshots-de-saldo.md) |
| Segurança e borda | [0007 Segurança](adr/0007-seguranca-e-protecao-de-dados.md) · [0014 BFF](adr/0014-bff-ponto-unico-de-entrada.md) · [0017 Sessão PKCE](adr/0017-sessao-do-bff-authorization-code-pkce.md) · [0018 Rate limit distribuído](adr/0018-rate-limit-distribuido.md) |
| Resiliência e operação | [0008 Resiliência, DLQ e cache](adr/0008-resiliencia-dlq-rate-limit-cache.md) · [0009 Observabilidade](adr/0009-observabilidade.md) · [0013 Capacidade](adr/0013-capacidade-e-dimensionamento.md) |
| Persistência e qualidade | [0019 EF por padrão, SQL medido](adr/0019-ef-por-padrao-sql-como-otimizacao-medida.md) · [0020 Qualidade estática (Sonar)](adr/0020-qualidade-estatica-sonarqube.md) |

## Teste e evidências
| Documento | Conteúdo |
|---|---|
| [`roteiro-de-teste-manual.md`](roteiro-de-teste-manual.md) | Passo a passo (PowerShell) com o resultado esperado em cada etapa |
| [`benchmarks/ef-vs-sql.md`](benchmarks/ef-vs-sql.md) | Medição EF × SQL otimizado |
| [`demos/upsert-demo.sh`](demos/upsert-demo.sh) | Demonstração do upsert em lote |
| [`../load/`](../load) | Scripts de carga (k6) |
