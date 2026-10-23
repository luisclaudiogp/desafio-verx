using Lancamentos.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Verx.IntegrationTests.Infra;
using Verx.Web.Hosting;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public class DatabaseMigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task BancoIndisponivel_TentaDeNovoEPropagaAFalhaNaUltimaTentativa()
    {
        await using var app = CriarApp("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");

        await Assert.ThrowsAnyAsync<Exception>(() => app.MigrateDatabaseAsync<LancamentosDbContext>(maxTentativas: 2));
    }

    [DockerFact]
    public async Task BancoDisponivel_AplicaAsMigrations()
    {
        await using var app = CriarApp(postgres.NovaConnectionString());

        await app.MigrateDatabaseAsync<LancamentosDbContext>();

        using var escopo = app.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<LancamentosDbContext>();
        Assert.Empty(await contexto.Database.GetPendingMigrationsAsync());
    }

    private static WebApplication CriarApp(string conexao)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<LancamentosDbContext>(o => o.UseNpgsql(conexao));
        return builder.Build();
    }
}
