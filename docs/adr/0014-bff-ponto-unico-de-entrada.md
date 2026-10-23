# ADR-0014 — BFF como ponto único de entrada (gateway + login)

- **Status:** Aceito
- **Data:** 2026-10-19

## Contexto
Até aqui o cliente falava com dois serviços diferentes (`Lancamentos.API` e `Consolidado.API`) e precisava obter o token
direto no Keycloak. Para uma aplicação (web, mobile, canal interno) consumir a solução isso significa:
- conhecer dois endereços e a topologia interna, que passa a fazer parte do contrato público;
- falar com o IdP e conhecer detalhes do OIDC (endpoint, client, grant);
- cada serviço publicado na borda: superfície de ataque, certificados, CORS e rate limit repetidos em todos.

## Decisão
Criar o **`Bff.API`** (*Backend for Frontend*), único componente publicado ao cliente. Os serviços de negócio ficam na rede
interna (sem porta no host).

**1) Login — ponte para o Keycloak.** `POST /auth/login`, `/auth/refresh`, `/auth/logout` e `GET /auth/me`.
O BFF é um *client* **confidencial** do Keycloak (`verx-bff`): o `client_secret` existe só no servidor e nunca chega ao navegador/app.
A integração fica atrás da porta `ITokenProvider` (adaptador `KeycloakTokenProvider`): trocar o fluxo (ex.: Authorization Code + PKCE
com sessão por cookie) não afeta controllers nem rotas.

**2) Gateway — roteamento.** [YARP](https://microsoft.github.io/reverse-proxy/) com rotas e clusters **em configuração** (`ReverseProxy`):

| Rota pública | Destino |
|---|---|
| `/api/contas/{contaId}/lancamentos/**` | `Lancamentos.API` |
| `/api/contas/{contaId}/saldo` e `/saldo-diario/**` | `Consolidado.API` |

O BFF **não contém regra de negócio nem acessa banco/broker** (verificado por teste de arquitetura): é só borda.

**3) Defesa em profundidade (zero trust).** O BFF valida o JWT (assinatura, emissor, audiência, expiração) e a posse da conta
(`conta_id` do token × `{contaId}` da rota) **antes** de encaminhar; o token segue adiante e os serviços **validam de novo**. Se o BFF
for contornado ou falhar, os serviços continuam protegidos.

**4) Resiliência na borda.**
- *Rate limit* por usuário e global nas rotas, e política própria para `/auth/*` por IP (20/min) contra força bruta.
- *Health check ativo* (`/health/live`) e passivo (falhas de transporte) por cluster; sem destino saudável a resposta é **imediata**
  (`503`) em vez de acumular requisições esperando timeout. `ActivityTimeout` de 30 s por requisição.
- *Chamada ao Keycloak:* timeout de 3 s por tentativa, 1 retry, circuit breaker (`Microsoft.Extensions.Http.Resilience`).
  Keycloak fora ⇒ `503` em `/auth/*` em ≤ 8 s; **tokens já emitidos continuam valendo** (as chaves JWKS ficam em cache).
- Corpo de requisição limitado a 1 MB; CORS só para as origens configuradas (`Cors:Origens`, vazio por padrão).

**5) Operação.** Traces (OpenTelemetry) atravessam o BFF até os serviços (o mesmo `traceparent`), métricas no Prometheus, `Idempotency-Key`
e `Authorization` são repassados sem alteração e o cabeçalho `Location` dos `201` aponta para o endereço do BFF.

**6) Contrato guardado por teste.** `GatewayRotasTests` compara por reflexão os controllers dos serviços com as rotas do BFF:
um endpoint novo sem rota, uma rota órfã, uma rota sem política de autorização/rate limit ou apontando para cluster inexistente
**quebram o build**.

## Alternativas consideradas
- **Cliente direto nos serviços (como estava)** — simples, mas expõe a topologia e repete borda em cada serviço.
- **Gateway pronto (Kong, Envoy, APIM, Ocelot)** — válidos em produção; YARP foi escolhido por rodar no mesmo stack .NET, ser extensível
  em C# (ex.: o handler de posse da conta) e dispensar mais um componente para o avaliador subir.
- **BFF que agrega/compõe respostas** — hoje não há tela que justifique composição; o BFF fica fino e pode ganhar endpoints de
  composição por canal quando houver necessidade real.
- **Authorization Code + PKCE no BFF (sessão por cookie)** — é o fluxo recomendado para navegadores e foi **implementado depois**, ver [ADR-0017](0017-sessao-do-bff-authorization-code-pkce.md). O *password grant* via BFF permanece para clientes de API e testes.

## Consequências
- (+) Um único endereço, um único ponto para TLS/WAF/CORS/rate limit/auditoria e para evoluir o contrato público.
- (+) Aplicações clientes não conhecem o Keycloak nem a topologia; o segredo do client fica no servidor.
- (+) Os serviços saem da rede pública (menor superfície); `docker-compose.direct.yml` os publica só para depuração/carga.
- (−) Mais um salto de rede: medido em ~+8 ms no p95 de escrita (14 → 22 ms a 800 req/s) em ambiente local ([ADR-0013](0013-capacidade-e-dimensionamento.md)).
- (−) O BFF vira ponto crítico de disponibilidade: é *stateless* e deve rodar com várias réplicas atrás de um balanceador.
- (−) `ROPC` está depreciado nas boas práticas OAuth 2.1 para clientes públicos; mitigado pelo client confidencial e pelo rate limit por IP distribuído ([ADR-0018](0018-rate-limit-distribuido.md)); navegadores usam o fluxo do ADR-0017.
