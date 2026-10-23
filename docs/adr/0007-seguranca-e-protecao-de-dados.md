# ADR-0007 — Segurança: JWT, autorização por conta e proteção de dados

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
Dados financeiros de clientes. Na versão anterior a validação de emissor e audiência do JWT estava desligada,
qualquer usuário autenticado acessava qualquer dado (risco de **BOLA/IDOR**), a API expunha um endpoint que
repassava credenciais ao Keycloak e havia segredos versionados.

## Decisão
**Autenticação** — Keycloak (OIDC). As APIs validam *assinatura, emissor, audiência e expiração* (`ClockSkew` de 30 s).
O emissor esperado (`Keycloak:Issuer`) é o endereço público; a descoberta de chaves usa a rede interna (`Keycloak:Authority`).
As APIs de negócio **não** emitem token. O login é feito pelo **BFF** ([ADR-0014](0014-bff-ponto-unico-de-entrada.md)), que o
repassa ao Keycloak como *client* confidencial (`verx-bff`, segredo só no servidor). O BFF valida o JWT e a posse da conta na borda e
os serviços **validam de novo** (zero trust). A audiência (`verx-api`) é a mesma para os dois *clients*.

**Autorização por recurso** — a claim multivalorada `conta_id` lista as contas do usuário. Toda rota é `/api/contas/{contaId}/...`
e um `AuthorizationHandler<ContaRequirement, Guid>` nega (`403`) quando a conta não pertence ao token. Isso fecha o BOLA por construção
(testes: `ContaAuthorizationHandlerTests`, e fluxo ponta a ponta com dois usuários).

**Proteção de dados**
- *Minimização:* o ledger guarda apenas `ContaId` (GUID pseudonimizado), tipo, valor e datas; **nenhum dado pessoal** (nome, CPF, e-mail) nos
  serviços de movimentação. A ligação conta↔pessoa fica no domínio de cadastro/IdP.
- *Logs sem valores:* mensagens de log não incluem valores nem identificadores pessoais; o handler global devolve `ProblemDetails`
  genérico em erros 5xx (sem *stack trace*).
- *Trânsito:* TLS terminado no gateway/ingress em produção (`RequireHttpsMetadata=true` fora de DEV); mTLS entre serviços é evolução.
- *Repouso:* criptografia do volume/TDE do PostgreSQL e do broker delegada à plataforma; criptografia em coluna (KMS) para
  campos sensíveis caso um dia sejam adicionados.
- *Segredos:* fora do código. No compose há valores **somente de desenvolvimento** sobrescrevíveis por `.env` (ignorado pelo git);
  em produção, cofre (Vault/Key Vault/Secrets Manager). Token do Sonar via variável de ambiente.
- *Abuso:* rate limit por usuário e global ([ADR-0008](0008-resiliencia-dlq-rate-limit-cache.md)); validação rigorosa de entrada no domínio.
- *Imagens:* execução como usuário não-root (`USER app`), imagens base oficiais, dependências com auditoria NuGet
  (`NU190x` tratado como erro de build — `TreatWarningsAsErrors`).

## Alternativas consideradas
- **Autorizar só por papel (role)** — não impede um usuário legítimo de ler a conta de outro.
- **Confiar no gateway para a autorização** — defesa em profundidade pede checagem também no serviço.
- **Manter o endpoint de token em cada API de negócio** — amplia superfície de ataque e manuseia senha de usuário; removido.
  Hoje existe **um** ponto, o BFF, com rate limit por IP, client confidencial, sem log de credenciais e atrás de uma porta (`ITokenProvider`).

## Consequências
- (+) Modelo simples, testável e verificável ponta a ponta.
- (−) O *password grant* do BFF existe **para clientes de API e testes**; o fluxo de navegador é Authorization Code + PKCE com sessão no servidor ([ADR-0017](0017-sessao-do-bff-authorization-code-pkce.md)). O `verx-api` (público) segue no realm só para os testes de carga.
- (−) Revogação imediata de token exige introspecção ou tokens de vida curta (hoje 15 min).
