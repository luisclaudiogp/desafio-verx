# ADR-0001 — Dois serviços (bounded contexts) em vez de monolito

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
O sistema legado "foi crescendo sem planejamento" e sofre com lentidão e instabilidade em horário de pico.
Há dois perfis de carga muito diferentes:
**escrita** de lançamentos (precisa ser sempre disponível e correta) e **leitura** de saldo (muito mais frequente,
tolera atraso de segundos, mas não pode derrubar a escrita).

## Decisão
Dois serviços independentes, cada um com seu banco e seu ciclo de deploy:

- **Lancamentos** — fonte da verdade (ledger). Escreve; publica eventos.
- **Consolidado** — projeção derivada do ledger. Lê; consome eventos.

Camadas internas (Clean Architecture): `Domain` ← `Application` ← `Infrastructure`/`API`.
Código compartilhado fica em `BuildingBlocks` (kernel, persistência, web) e `Shared.Contracts` (apenas eventos).

## Alternativas consideradas
- **Monolito modular com um único banco** — mais simples de operar e permitiria transação única, mas acopla a
  disponibilidade do ledger à carga de leitura (o problema que o banco quer resolver) e impede escalar leitura e escrita
  de forma independente. Seria a minha escolha para um time pequeno **sem** esse requisito de isolamento.
- **Mais serviços (contas, extrato, notificações...)** — fragmentação prematura; não há domínio que justifique agora.
- **Compartilhar o banco entre os serviços** — cria acoplamento de schema oculto; rejeitado.

## Consequências
- (+) A queda/lentidão do Consolidado **não afeta** o registro de lançamentos (nenhuma chamada síncrona entre eles).
- (+) Cada lado escala e otimiza seu armazenamento isoladamente.
- (−) Consistência **eventual** entre ledger e saldo (segundos). Mitigada e documentada em [ADR-0004](0004-consistencia-idempotencia-concorrencia.md).
- (−) Mais componentes para operar; compensado por Docker Compose, health checks e observabilidade.
- Validação: `ArchitectureTests.BoundedContexts_SaoIndependentes` impede referência cruzada entre contextos.
