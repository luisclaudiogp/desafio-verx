# ADR-0018 — Rate limit distribuído (Redis)

- **Status:** Aceito (substitui a limitação "por instância" citada nos ADRs 0008 e 0013)
- **Data:** 2026-10-20

## Contexto
O rate limit era um *token bucket* **em memória**, por instância. Com N réplicas o limite efetivo vira N× o configurado, e um abusador
que cai em réplicas diferentes multiplica a própria cota. Para o login (`/auth/*`) isso enfraquece a proteção contra força bruta.

## Decisão
Um *token bucket* **compartilhado no Redis**, atrás de uma porta:

- `ITokenBucketStore.TentarConsumirAsync(chave, capacidade, reposicaoPorSegundo)` → permitido ou "tente em X ms".
- `RedisTokenBucketStore`: um **script Lua atômico** (relógio do próprio Redis, sem depender do relógio das réplicas), 1 ida ao Redis por
  decisão, chave com expiração (`2 × tempo para encher`, sem lixo).
- `LocalTokenBucketStore`: o mesmo algoritmo em memória (baldes ociosos são removidos).
- `ResilientTokenBucketStore` (decorator): timeout de 100 ms + *circuit breaker* (Polly). Redis lento ou fora ⇒ **fail-open para o limite
  local** da instância, e a requisição segue; o circuito evita pagar o timeout a cada chamada.
- `TokenBucketStoreRateLimiter`: adaptador para o `RateLimiter` do ASP.NET (middleware, políticas e `Retry-After` inalterados).

**Camadas.** A porta, o algoritmo local e o adaptador para o ASP.NET ficam em `Verx.Web`, que **não** conhece Redis nem Polly (regra de arquitetura em teste). O adaptador Redis e o decorator resiliente ficam em `Verx.Web.Redis`, que só o BFF referencia.

**Contrato do `RateLimiter`.** O middleware do ASP.NET tenta sempre `AttemptAcquire` (síncrono) antes de `AcquireAsync`. Como a decisão exige E/S, o limitador **não bloqueia a thread**: o caminho síncrono responde só o que já sabe (esgotado até *X*, via cache de negativas do próprio limitador; ou "ainda não esgotado" para `permitCount = 0`) e, no resto, devolve "não decidido", deixando a decisão para o caminho assíncrono. O cache de negativas também poupa o Redis sob ataque (nenhuma ida ao Redis enquanto o balde está vazio). A alternativa, bloquear com *sync-over-async*, foi descartada por risco de esgotar o *thread pool* quando o Redis degrada.

Ativação **por configuração**: `RateLimiting:Redis:ConnectionString` (e `RateLimiting:Redis:TimeoutMs`). Sem ela, tudo funciona como antes (apenas local). No compose o
**BFF** usa o Redis (a borda é onde o limite global importa); os serviços internos mantêm o limite local como última linha de defesa.
A política de login do BFF (20/min por IP) usa o mesmo mecanismo.

## Alternativas consideradas
- **Janela fixa com `INCR` + `EXPIRE`** — simples, mas permite o dobro da cota na virada da janela e não reproduz o *burst* do token bucket.
- **Limitar no balanceador/WAF** — adequado e complementar em produção; aqui o limite por usuário autenticado exige o token validado.
- **Fail-closed quando o Redis cai** — protegeria mais, porém transformaria uma falha do cache em indisponibilidade total.
- **Biblioteca pronta** — a lógica é pequena e precisa conviver com as políticas e o `Retry-After` do ASP.NET.

## Consequências
- (+) Limite **exato** com qualquer número de réplicas. Provado: duas instâncias do BFF juntas aceitaram exatamente 20 logins e
  recusaram os 4 excedentes (`429`); em teste de integração, 2 réplicas × 60 tentativas contra cota de 40 ⇒ exatamente 40 permitidas
  (com limite local seriam 80).
- (+) Falha do Redis não derruba a API (testado: Redis parado ⇒ a cota local continua valendo).
- (−) +1 ida ao Redis por requisição para cada limitador (global e por usuário). Medido a quente: escrita via BFF 800 req/s ⇒ p95 20 ms;
  leitura 1.500 req/s ⇒ p95 8 ms, 0 % de erro.
- (−) O limite global é uma única chave quente no Redis; em volumes muito maiores, particionar a cota global entre N chaves.
- (−) Com o Redis fora, o limite volta a ser por instância até o circuito fechar.
