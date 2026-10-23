# Roteiro de teste manual (PowerShell, passando só pelo BFF)

Passo a passo para validar a solução ponta a ponta antes de subir o código: login, lançamentos, idempotência, estorno, saldos,
segurança e falhas. Use sempre `http://localhost:5000` (o BFF, único endereço público). Rode os blocos no **mesmo terminal**, na raiz do
repositório, porque as variáveis (`$t`, `$h`, `$conta`) são reaproveitadas entre os passos.

## Como usar este roteiro (copiar e colar)

Você não precisa digitar nada: cada bloco de código abaixo pode ser **copiado e colado** no PowerShell, na ordem, acompanhando o resultado.

1. **Abra um PowerShell na raiz do repositório** (a pasta onde estão `docker-compose.yml` e `Verx.Finance.slnx`).
   No Windows: abra a pasta no Explorer, clique na barra de endereço, digite `powershell` e tecle Enter. No VS Code: `Ctrl+'` (terminal integrado).
2. **Copie o bloco**: no GitHub passe o mouse sobre o bloco e clique no ícone de copiar (canto superior direito); em um editor, selecione o bloco e use `Ctrl+C`.
3. **Cole no PowerShell** com `Ctrl+V` (ou clique com o botão direito do mouse, que cola no terminal clássico) e tecle **Enter**. Se o bloco tiver várias linhas, o PowerShell executa uma a uma.
4. **Compare com o "Esperado"** que vem logo abaixo de cada bloco antes de seguir para o próximo.
5. **Use sempre o mesmo terminal**: as variáveis (`$b`, `$conta`, `$t`, `$h`, `$suf`, `$c1`, `$d1`) criadas num passo são usadas nos seguintes. Se abrir um terminal novo, volte ao passo **1. Login** e refaça a partir dele.
6. Os comentários no fim das linhas (`# 401`) indicam o código HTTP esperado; podem ser colados junto, o PowerShell os ignora.

Dicas: se a colagem pedir confirmação por conter várias linhas, aceite; `Ctrl+C` **dentro do PowerShell interrompe** o comando em andamento (não copia). Se o token expirar
(15 minutos), repita o bloco do passo 1. Os passos de resiliência (8) derrubam serviços de propósito; ao terminar, `docker compose start` ou o `up -d` do passo 0 os restaura.
Requisitos: Docker em execução, .NET SDK 10 e PowerShell (o do Windows 10/11 ou o PowerShell 7).

## 0. Pré-condições

```powershell
dotnet build Verx.Finance.slnx
dotnet test Verx.Finance.slnx
docker compose down -v
docker compose up -d --build
docker compose ps
```

- **Esperado:** build com 0 erros e 0 warnings; testes todos verdes (os de integração exigem Docker).
- O `down -v` apaga os dados e garante que o Keycloak reimporte o realm com o client `verx-bff`.
- Espere ~1 minuto até `docker compose ps` mostrar tudo `healthy` ou `running`.

```powershell
(iwr -UseBasicParsing http://localhost:5000/health/live).StatusCode
```
**Esperado:** `200`.

## 1. Login

```powershell
$b = "http://localhost:5000"
$conta = "11111111-1111-1111-1111-111111111111"

$login = irm -Method Post "$b/auth/login" -ContentType "application/json" `
  -Body '{"usuario":"cliente.a","senha":"dev-only-a"}'
$t = $login.access_token
$h = @{ Authorization = "Bearer $t" }
$login.expires_in
irm "$b/auth/me" -Headers $h
```
**Esperado:** `expires_in` = 900, e `/auth/me` devolve o `usuario` e a lista de contas com `1111…`.

Testes negativos:
```powershell
try { irm -Method Post "$b/auth/login" -ContentType "application/json" -Body '{"usuario":"cliente.a","senha":"errada"}' } catch { $_.Exception.Response.StatusCode.value__ }   # 401
try { irm -Method Post "$b/auth/login" -ContentType "application/json" -Body '{}' } catch { $_.Exception.Response.StatusCode.value__ }                                            # 400
try { irm "$b/api/contas/$conta/saldo" } catch { $_.Exception.Response.StatusCode.value__ }                                                                                         # 401 (sem token)
```

## 2. Registrar lançamentos

```powershell
function Lancar($tipo, $valor, $chave) {
  irm -Method Post "$b/api/contas/$conta/lancamentos" -Headers ($h + @{ "Idempotency-Key" = $chave }) `
    -ContentType "application/json" -Body (@{ tipo = $tipo; valor = $valor } | ConvertTo-Json)
}
$suf = Get-Random
$c1 = Lancar "Credito" 100.00 "demo-$suf-1"
$d1 = Lancar "Debito"   30.00 "demo-$suf-2"
$c1; $d1
```
**Esperado:** dois lançamentos criados (201), com `id`, tipo e valor.

## 3. Idempotência (retry seguro)

```powershell
$r = iwr -UseBasicParsing -Method Post "$b/api/contas/$conta/lancamentos" -Headers ($h + @{ "Idempotency-Key" = "demo-$suf-1" }) `
  -ContentType "application/json" -Body '{"tipo":"Credito","valor":100.00}'
$r.StatusCode; $r.Headers["Idempotent-Replayed"]
```
**Esperado:** `200` e `Idempotent-Replayed: true`, com o **mesmo** `id` de `$c1`, sem criar outro lançamento.

### 3.1 `Idempotency-Key` reutilizada com outro corpo

```powershell
try {
  irm -Method Post "$b/api/contas/$conta/lancamentos" -Headers ($h + @{ "Idempotency-Key" = "demo-$suf-1" }) `
    -ContentType "application/json" -Body '{"tipo":"Credito","valor":999.00}'
} catch { $_.Exception.Response.StatusCode.value__ }
```
**Esperado:** `422`. A mesma chave com outro valor **não** é tratada como replay (o valor 999 nunca foi registrado).

## 4. Consultar o saldo (consistência eventual: ~1–2 s)

```powershell
Start-Sleep 3
$hoje = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
irm "$b/api/contas/$conta/saldo" -Headers $h
irm "$b/api/contas/$conta/saldo-diario/$hoje" -Headers $h
$passado = (Get-Date).ToUniversalTime().AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
irm "$b/api/contas/$conta/saldo?em=$passado" -Headers $h
```
**Esperado:**
- `saldo` e `saldo-diario` incluem os 100 de crédito e os 30 de débito (+70 sobre o saldo anterior).
- O saldo "em 1 hora atrás" **não** inclui esses lançamentos.
- Guarde o saldo atual: `$antes = (irm "$b/api/contas/$conta/saldo" -Headers $h).saldo`.

## 4.1 Saldo insuficiente

```powershell
$saldoAtual = (irm "$b/api/contas/$conta/saldo" -Headers $h).saldo
try { Lancar "Debito" ($saldoAtual + 1000) "sem-saldo-$suf" } catch { $_.Exception.Response.StatusCode.value__ }
```
**Esperado:** `422` ("Saldo insuficiente"). Nada é gravado e nenhum evento é publicado: o saldo e o ledger continuam iguais.
Um débito **igual ou menor** ao saldo é aceito.

## 5. Estorno

```powershell
$e = irm -Method Post "$b/api/contas/$conta/lancamentos/$($d1.id)/estornos" -Headers ($h + @{ "Idempotency-Key" = "est-$suf" })
$e
try { irm -Method Post "$b/api/contas/$conta/lancamentos/$($d1.id)/estornos" -Headers ($h + @{ "Idempotency-Key" = "est2-$suf" }) } catch { $_.Exception.Response.StatusCode.value__ }
Start-Sleep 3
(irm "$b/api/contas/$conta/saldo" -Headers $h).saldo
irm "$b/api/contas/$conta/lancamentos/$($c1.id)" -Headers $h
```
**Esperado:**
- O estorno é um lançamento novo, compensatório (crédito de 30).
- O segundo estorno do mesmo lançamento é **recusado** (422).
- O saldo sobe 30 em relação ao do passo 4.
- A consulta do lançamento original devolve os dados.

## 6. Segurança e validações

```powershell
$outra = "22222222-2222-2222-2222-222222222222"
try { irm "$b/api/contas/$outra/saldo" -Headers $h } catch { $_.Exception.Response.StatusCode.value__ }                                   # 403 (conta de outro cliente)
try { irm -Method Post "$b/api/contas/$conta/lancamentos" -Headers $h -ContentType "application/json" -Body '{"tipo":"Credito","valor":5}' } catch { $_.Exception.Response.StatusCode.value__ }   # 400 (sem Idempotency-Key)
try { Lancar "Credito" -5 "neg-$suf" } catch { $_.Exception.Response.StatusCode.value__ }                                               # 422 (valor inválido)
```
Troque de usuário: faça login como `cliente.b` / `dev-only-b` e confirme que ele acessa só a conta `2222…` e recebe 403 na `1111…`.

## 6.1 Login de navegador (Authorization Code + PKCE): verificação pelo PowerShell

O BFF também implementa o fluxo de navegador (Authorization Code + PKCE, sessão no servidor, [ADR-0017](adr/0017-sessao-do-bff-authorization-code-pkce.md)). Este roteiro não exige
navegador: o fluxo completo é coberto por testes automatizados, e aqui você confirma as duas propriedades principais pela linha de comando.

```powershell
# a) O Keycloak recusa a autorização sem PKCE (sem code_challenge)
curl.exe -s -i "http://localhost:8081/realms/Verx/protocol/openid-connect/auth?client_id=verx-bff&response_type=code&scope=openid&redirect_uri=http%3A%2F%2Flocalhost%3A5000%2Fsignin-oidc" | Select-String "error="

# b) O BFF inicia o login redirecionando (302) para o Keycloak, com PAR (request_uri) e cookies de correlação
curl.exe -s -i "$b/bff/login?returnUrl=/auth/me" | Select-String "HTTP/1.1|^Location"
```
**Esperado:**
- (a) `error=invalid_request` e `Missing parameter: code_challenge_method`.
- (b) `HTTP/1.1 302 Found` e `Location` apontando para `.../openid-connect/auth?client_id=verx-bff&request_uri=...`.

## 7. Renovar token e sair

```powershell
$rt = $login.refresh_token
$novo = irm -Method Post "$b/auth/refresh" -ContentType "application/json" -Body (@{ refreshToken = $rt } | ConvertTo-Json)
$novo.access_token.Length
iwr -UseBasicParsing -Method Post "$b/auth/logout" -ContentType "application/json" -Body (@{ refreshToken = $novo.refresh_token } | ConvertTo-Json) | % StatusCode   # 204
try { irm -Method Post "$b/auth/refresh" -ContentType "application/json" -Body (@{ refreshToken = $novo.refresh_token } | ConvertTo-Json) } catch { $_.Exception.Response.StatusCode.value__ }   # 401
```

## 8. Resiliência

**a) Consolidado e RabbitMQ fora: a escrita continua**
```powershell
docker compose stop consolidado-api rabbitmq
Lancar "Credito" 10.00 "res-$suf-1"; Lancar "Credito" 10.00 "res-$suf-2"      # continuam 201
docker compose start rabbitmq; Start-Sleep 15; docker compose start consolidado-api; Start-Sleep 15
(irm "$b/api/contas/$conta/saldo" -Headers $h).saldo                             # reflete +20
```
**b) Redis fora:** `docker compose stop redis`; o `saldo-diario` continua respondendo 200 (mais lento). Depois `docker compose start redis`.

**c) Lançamentos fora:** `docker compose stop lancamentos-api`. O POST dá 502/503 e o `GET saldo` continua 200. Depois `docker compose start lancamentos-api`.

**d) Keycloak fora:** `docker compose stop keycloak`. O login dá 503 em até ~8 s, e o token que você já tem continua funcionando. Depois `docker compose start keycloak`.

## 9. Observabilidade e filas

| O quê | Onde | Esperado |
|---|---|---|
| Traces | http://localhost:16686 (serviço `Bff.API`) | Um trace com spans do BFF e do Lancamentos/Consolidado |
| Fila | http://localhost:15672 (`verx` / `verx_dev_only`) | `consolidado.lancamento-criado` com 0 mensagens e sem fila `_error` |
| Métricas | http://localhost:9090/targets | `bff`, `lancamentos-api` e `consolidado-api` em `up` |
| Documentação interativa | http://localhost:5000/scalar/v1 | Cole o token em **Authentication → Bearer** e teste pela tela |

## 9.1 Snapshots de saldo (opcional)

Os snapshots cobrem **dias encerrados**; com dados só de hoje não há o que gerar. Para ver o efeito, lance com `ocorridoEm` em dias passados
(`{"tipo":"Credito","valor":10,"ocorridoEm":"2026-10-10T12:00:00Z"}`), aguarde o próximo ciclo (`Snapshots:IntervaloSegundos`, padrão 600 s; para
testar, suba o Consolidado com `Snapshots__IntervaloSegundos=10`) e confira:
```powershell
docker compose exec -T postgres psql -U postgres -d consolidado_db -c 'select "ContaId","Data","TotalCreditos","TotalDebitos" from saldo_snapshots order by "Data"'
```
Em seguida consulte `GET /saldo?em=<instante>`: o valor é igual ao de antes dos snapshots (a consulta só muda de caminho, não de resultado).

## 10. Rate limit (opcional)

```powershell
1..30 | % { try { (iwr -UseBasicParsing -Method Post "$b/auth/login" -ContentType "application/json" -Body '{"usuario":"x","senha":"y"}').StatusCode } catch { $_.Exception.Response.StatusCode.value__ } } | group | select Name, Count
```
**Esperado:** as primeiras 20 chamadas dão 401 e as seguintes **429** (limite de 20 logins por minuto por IP). Aguarde 1 minuto antes de logar de novo.

O limite é **distribuído** (Redis): com duas instâncias do BFF ele continua valendo no total. Para ver:
```powershell
docker compose run -d --no-deps --name bff2 -p 5002:8080 bff
# 12 tentativas em :5000 e 12 em :5002 => 20 respostas 401 e 4 respostas 429 no total
docker rm -f bff2
```

## Encerrar

```powershell
docker compose down
```
