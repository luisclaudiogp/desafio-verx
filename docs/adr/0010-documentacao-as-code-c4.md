# ADR-0010 — Documentação como código (ADR + C4 em Markdown/Mermaid)

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
A documentação anterior estava em PDF/`.drawio`/HTML: difícil de versionar, revisar em *pull request* e manter sincronizada com o código.

## Decisão
- **Decisões** em ADRs (`docs/adr`), uma por arquivo, imutáveis após aceitas.
- **Diagramas** no **modelo C4** (Contexto, Containers, Componentes) escritos como código: **Mermaid** dentro do Markdown
  (renderiza no GitHub, sem ferramenta extra) e **Structurizr DSL** (`docs/c4/workspace.dsl`) como fonte para explorar o modelo.
- **Regras arquiteturais executáveis** (`Verx.ArchitectureTests`) para que o diagrama não divirja da realidade.
- Documentos vivem no repositório e mudam junto com o código na mesma PR.

## Alternativas consideradas
- **PDF/Word/Drawio** — sem diff legível; descartados.
- **Confluence/Wiki externo** — foge do requisito de manter tudo no repositório.

## Consequências
- (+) Revisão, histórico e *blame* da arquitetura; onboarding mais rápido.
- (−) Mermaid C4 ainda tem recursos de layout limitados (o DSL cobre quem precisar de mais).
