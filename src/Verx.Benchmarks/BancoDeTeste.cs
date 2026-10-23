using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Verx.Benchmarks;

public static class BancoDeTeste
{
    public static async Task<BancoTemporario<TContext>> CriarAsync<TContext>(
        string conexaoBase, Func<DbContextOptions<TContext>, TContext> fabrica) where TContext : DbContext
    {
        var nome = "bench_" + Guid.NewGuid().ToString("N");
        var administrativa = new NpgsqlConnectionStringBuilder(conexaoBase) { Database = "postgres" }.ConnectionString;
        var daBase = new NpgsqlConnectionStringBuilder(conexaoBase) { Database = nome, MaxPoolSize = 100 }.ConnectionString;

        await BancoTemporario<TContext>.ExecutarAsync(administrativa, $"CREATE DATABASE \"{nome}\"");
        var banco = new BancoTemporario<TContext>(administrativa, nome, daBase, fabrica);
        await using var contexto = banco.Fabrica();
        await contexto.Database.MigrateAsync();
        await BancoTemporario<TContext>.ExecutarAsync(administrativa, "CHECKPOINT");
        return banco;
    }
}
