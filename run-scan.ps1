# Analise estatica local com SonarQube (opcional).
# Uso: $env:SONAR_TOKEN = "<token gerado em http://localhost:9000>"; .\run-scan.ps1
# Suba o Sonar antes: docker compose --profile quality up -d sonarqube
param([string]$HostUrl = "http://localhost:9000")

if (-not $env:SONAR_TOKEN) { throw "Defina a variavel de ambiente SONAR_TOKEN (nunca versione o token)." }

dotnet tool install --global dotnet-sonarscanner 2>$null

# Migrations do EF sao codigo gerado: ficam fora da analise.
dotnet sonarscanner begin /k:"Verx-Finance" /n:"Verx Finance" /d:sonar.host.url=$HostUrl /d:sonar.token=$env:SONAR_TOKEN `
  /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" `
  /d:sonar.coverage.exclusions="**/Program.cs,**/Migrations/**,**/*.csproj,load/**" `
  /d:sonar.exclusions="**/Migrations/**"
# relatorios de cobertura de execucoes anteriores apontam para linhas que podem nao existir mais
Get-ChildItem -Path src -Recurse -Directory -Filter TestResults | Remove-Item -Recurse -Force
dotnet build Verx.Finance.slnx
dotnet test Verx.Finance.slnx --no-build --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet sonarscanner end /d:sonar.token=$env:SONAR_TOKEN
