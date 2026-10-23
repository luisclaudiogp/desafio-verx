using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Verx.Benchmarks;

public sealed class BancoTemporario<TContext> : IAsyncDisposable where TContext : DbContext
{
    private readonly string _administrativa;
    private readonly string _nome;
    private readonly DbContextOptions<TContext> _opcoes;
    private readonly Func<DbContextOptions<TContext>, TContext> _fabrica;

    internal BancoTemporario(
        string administrativa, string nome, string conexao, Func<DbContextOptions<TContext>, TContext> fabrica)
    {
        _administrativa = administrativa;
        _nome = nome;
        _fabrica = fabrica;
        _opcoes = new DbContextOptionsBuilder<TContext>().UseNpgsql(conexao).Options;
    }

    public Func<TContext> Fabrica => () => _fabrica(_opcoes);

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecutarAsync(_administrativa, $"DROP DATABASE IF EXISTS \"{_nome}\" WITH (FORCE)");
    }

    internal static async Task ExecutarAsync(string conexao, string comando)
    {
        await using var conn = new NpgsqlConnection(conexao);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(comando, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
