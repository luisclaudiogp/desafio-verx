FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Shared.Contracts/ src/Shared.Contracts/
COPY src/Consolidado/Consolidado.API/ src/Consolidado/Consolidado.API/
COPY src/Consolidado/Consolidado.Application/ src/Consolidado/Consolidado.Application/
COPY src/Consolidado/Consolidado.Domain/ src/Consolidado/Consolidado.Domain/
COPY src/Consolidado/Consolidado.Infrastructure/ src/Consolidado/Consolidado.Infrastructure/
RUN dotnet publish src/Consolidado/Consolidado.API/Consolidado.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Consolidado.API.dll"]
