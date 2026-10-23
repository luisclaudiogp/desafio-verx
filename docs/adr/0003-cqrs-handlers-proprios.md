# ADR-0003 — CQRS com handlers próprios (sem MediatR)

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
Escrita e leitura têm necessidades opostas (consistência/invariantes vs. velocidade/denormalização).
Na primeira versão da solução, o CQRS era citado na documentação mas o código tinha um único `Service` que misturava
comando, consulta, cache e regras. Aqui o padrão é **implementado**.

## Decisão
- **Commands** (`RegistrarLancamentoCommand`, `EstornarLancamentoCommand`, `RegistrarMovimentosEmLoteCommand`) passam por
  `ICommandHandler<TCommand,TResult>` e alteram estado via agregados do domínio.
- **Queries** (`ObterLancamentoQuery`, `ObterSaldoDiarioQuery`, `ObterSaldoEmQuery`) passam por `IQueryHandler<,>`, sem efeitos
  colaterais, lendo por portas próprias e segregadas (`ISaldoDiarioReadRepository`, `ISaldoEmReadRepository`) com projeções `AsNoTracking`.
- Separação física: o **Consolidado é o read model** (banco próprio, desnormalizado) alimentado por eventos do write model.
- Contratos mínimos (`ICommand`, `IQuery`, handlers) vivem em `Verx.SharedKernel`; os controllers recebem o handler específico
  por injeção (dependência explícita, sem *service locator*).

## Alternativas consideradas
- **MediatR** — padrão comum, mas desde 2025 tem licença comercial; para três interfaces triviais não compensa a dependência.
  Trocar depois é mecânico (os handlers já têm o formato esperado) caso se queira *pipelines* (validação, logging).
- **Manter `Service` único** — foi o que gerou componentes com múltiplas responsabilidades.
- **Event sourcing no write side** — fora do escopo; ver ADR-0005 sobre o ledger.

## Consequências
- (+) Cada handler tem **uma responsabilidade**; regras de negócio ficam no domínio (`Lancamento`, `SaldoDiario.Aplicar`),
  não na aplicação.
- (+) Decorators na leitura (ex.: cache — [ADR-0008](0008-resiliencia-dlq-rate-limit-cache.md)) sem tocar nos handlers.
- (−) Mais arquivos/tipos que um service único; aceito em troca de coesão (e um tipo por arquivo, regra do projeto).
- (−) Sem *pipeline behaviors* automáticos; validação transversal hoje está no domínio e na borda HTTP.
