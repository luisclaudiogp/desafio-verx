# ADR-0020 — Qualidade estática contínua: SonarQube, quality gate e revisão de hotspots

- **Status:** Aceito
- **Data:** 2026-10-23

## Contexto
Testes e regras arquiteturais ([ADR-0010](0010-documentacao-as-code-c4.md)) garantem comportamento e dependências, mas não pegam
*code smells*, testes sem assertion, laços de retry confusos ou credenciais literais. Um feedback anterior também apontou
manutenibilidade (responsabilidades múltiplas, regra de negócio na camada errada); era preciso um critério objetivo e repetível.

## Decisão
- Análise estática com **SonarQube** (perfil `quality` do Compose; `run-scan.ps1` faz build + testes com cobertura OpenCover + análise).
- Meta: **0 bugs, 0 vulnerabilidades, 0 code smells, 0 hotspots pendentes** e quality gate verde (inclui cobertura mínima no código novo).
- **Hotspots** são *revisados*, não ignorados: cada um tem decisão registrada (seguro/corrigido) com justificativa — hoje todos são
  credenciais de desenvolvimento (`verx_dev_only`, `dev-only-*`), `Math.random` em dados de carga (k6/benchmark) e `http://` na rede interna do Compose.
- **Fora da análise:** *migrations* do EF (código gerado) e, da cobertura, `load/` (scripts k6) e `Program.cs`.
- Achados que viraram mudança de código: laços de retry com condição de parada explícita (a última tentativa propaga a falha),
  `SaldosController` dividido por responsabilidade, remoção dos marcadores `ICommand`/`IQuery` sem uso,
  totalização de movimentos movida para o domínio (`SaldoDiario.AcumularPorDia`), testes com assertion explícita.

## Alternativas consideradas
- **Só analisadores Roslyn (`TreatWarningsAsErrors`)** — já ativos, mas não cobrem duplicação, cobertura nem hotspots de segurança.
- **SonarCloud** — exigiria conta/token externos; o SonarQube local reproduz o resultado sem depender de terceiros.
- **Suprimir regras globalmente** — esconde o problema; preferiu-se corrigir ou revisar caso a caso.

## Consequências
- (+) Critério objetivo e reprodutível de qualidade; regressões de manutenibilidade aparecem cedo.
- (−) Os hotspots revisados vivem no servidor Sonar; num servidor novo reaparecem como "a revisar" (as justificativas estão aqui).
- (−) Ainda não roda no CI (evolução: publicar cobertura/Sonar no pipeline — ver README).
- Como validamos: `docker compose --profile quality up -d sonarqube` e `.\run-scan.ps1`; o quality gate deve ficar **OK**.
