# ADR-0017 — Sessão do BFF com Authorization Code + PKCE

- **Status:** Aceito (complementa o [ADR-0014](0014-bff-ponto-unico-de-entrada.md))
- **Data:** 2026-10-20

## Contexto
O BFF oferecia apenas `POST /auth/login` (*password grant*): o navegador (ou app) enviava a senha ao BFF e guardava os tokens. Para
aplicações web isso é inadequado: a senha passa pela aplicação e o JWT fica acessível ao JavaScript (roubo por XSS).

## Decisão
Adicionar o fluxo recomendado para navegadores, **Authorization Code + PKCE**, com **sessão no servidor**; o *password grant*
continua para clientes de API/teste.

| Endpoint | Função |
|---|---|
| `GET /bff/login?returnUrl=` | Redireciona ao Keycloak (código + PKCE S256; `returnUrl` só aceito se for local, contra *open redirect*) |
| `GET /signin-oidc` | *Callback*: o BFF troca o `code` (com `code_verifier` e o segredo do client) e abre a sessão |
| `POST /bff/logout` | Encerra a sessão local e a do Keycloak (*end_session*) |
| `GET /auth/me` | Funciona com Bearer **ou** cookie (esquema de política) |

**Os tokens nunca chegam ao navegador.** O cookie (`HttpOnly`, `SameSite=Lax`, `Secure` fora de dev) guarda só uma **chave opaca**; o
bilhete (access/refresh/id token) fica no **Redis** (`RedisTicketStore`, expiração por inatividade de 30 min). O YARP injeta o
`Authorization: Bearer` da sessão ao encaminhar (`TokenDaSessaoRequestTransform`); se o cliente já enviou um Bearer, ele prevalece.

**Renovação transparente.** `RenovadorDeSessao` renova o access token pelo *refresh token* quando faltam menos de 60 s para expirar
(reaproveitando `ITokenProvider`); o bilhete atualizado é regravado no Redis. Refresh recusado ⇒ sessão encerrada; Keycloak
indisponível com token ainda válido ⇒ a sessão continua.

**Escala horizontal.** Chaves do *Data Protection* em Redis (várias réplicas leem o mesmo cookie) e sessão em Redis: o BFF segue
*stateless*.

**CSRF.** Requisições que alteram estado e autenticadas **por cookie** exigem o header `X-CSRF` (um header customizado força
*preflight* CORS) ou `Sec-Fetch-Site: same-origin`; senão `403`. Com Bearer a proteção não se aplica (não há credencial ambiente).
O logout é `POST` pelo mesmo motivo.

**Configuração do Keycloak.** O client `verx-bff` agora tem fluxo padrão habilitado, `redirectUris` exato, **PKCE S256 obrigatório**
(`pkce.code.challenge.method`; provado: uma autorização sem PKCE é recusada com `invalid_request`) e a claim `conta_id` também no
*id token* (o principal do cookie vem dele; o access token fica só no servidor). O .NET usa **PAR** (*pushed authorization request*)
automaticamente: os parâmetros e o `code_challenge` vão pelo *back-channel*, não pela URL.

**Rede.** O Keycloak anuncia o endereço público (`localhost:8081`); dentro do Docker o BFF o alcança em `keycloak:8080`. Um
`DelegatingHandler` reescreve o host só nas chamadas de *back-channel* do OIDC; os redirecionamentos do navegador continuam públicos.

## Alternativas consideradas
- **Tokens no `localStorage` / SPA falando direto com o Keycloak** — expõe o JWT a XSS; é justamente o que o padrão BFF evita.
- **Cookie com o bilhete criptografado (sem armazenamento)** — mais simples, mas leva os tokens ao navegador (embora cifrados) e
  estoura o limite de tamanho do cookie com muitas contas; e não permite revogar a sessão no servidor.
- **`response_mode=form_post`** (padrão do handler) — exige cookies `SameSite=None; Secure`, que quebram em HTTP local; o `query`
  do *code flow* é suficiente e mais simples.
- **Remover o *password grant*** — quebraria testes e ferramentas sem navegador; segue disponível, com rate limit por IP.

## Consequências
- (+) Senha digitada só na tela do Keycloak; JWT fora do alcance do JavaScript; revogação de sessão no servidor; renovação transparente.
- (+) Validado ponta a ponta com um cliente que age como navegador: login → code → sessão → chamadas roteadas → CSRF → logout;
  e a renovação alterando o bilhete no Redis a cada requisição quando forçada.
- (−) Sessões dependem do Redis (indisponível ⇒ novos logins por navegador falham; Bearer/API seguem). Documentado como *fail-closed*.
- (−) Atrás de um balanceador real é preciso `ForwardedHeaders` e TLS terminado corretamente (o `redirect_uri` usa o host da requisição).
- (−) Renovações simultâneas da mesma sessão podem disputar o *refresh token* se o Keycloak estiver com rotação ativa; hoje a rotação
  está desligada. Um *lock* por sessão é a evolução.
