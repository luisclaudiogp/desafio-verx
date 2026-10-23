FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Shared.Contracts/ src/Shared.Contracts/
COPY src/Lancamentos/Lancamentos.API/ src/Lancamentos/Lancamentos.API/
COPY src/Lancamentos/Lancamentos.Application/ src/Lancamentos/Lancamentos.Application/
COPY src/Lancamentos/Lancamentos.Domain/ src/Lancamentos/Lancamentos.Domain/
COPY src/Lancamentos/Lancamentos.Infrastructure/ src/Lancamentos/Lancamentos.Infrastructure/
RUN dotnet publish src/Lancamentos/Lancamentos.API/Lancamentos.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Lancamentos.API.dll"]
