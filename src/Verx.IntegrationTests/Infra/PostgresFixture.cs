using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Verx.IntegrationTests.Infra;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public async Task InitializeAsync()
    {
        if (!DockerFactAttribute.DockerDisponivel) return;

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithCommand("-c", "max_connections=500")
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public async Task<Func<TContext>> NovoBancoAsync<TContext>(Func<DbContextOptions<TContext>, TContext> fabrica)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>().UseNpgsql(NovaConnectionString()).Options;
        await using (var migracao = fabrica(options))
            await migracao.Database.MigrateAsync();
        return () => fabrica(options);
    }

    public string NovaConnectionString()
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(_container!.GetConnectionString())
        {
            Database = "t_" + Guid.NewGuid().ToString("N"),
            Pooling = true,
            MaxPoolSize = 30
        };
        return builder.ConnectionString;
    }
}
