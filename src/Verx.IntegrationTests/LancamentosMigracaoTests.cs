using Lancamentos.Domain.Entities;
using Lancamentos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Verx.IntegrationTests.Infra;
using Verx.SharedKernel;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public class LancamentosMigracaoTests(PostgresFixture postgres)
{
    private static readonly Guid Conta = Guid.NewGuid();
    private static readonly DateTimeOffset Quando = new(2026, 8, 3, 14, 30, 15, 123, TimeSpan.Zero);

    [DockerFact]
    public async Task DadosDaVersaoAnterior_GanhamPosicaoEHashIdenticosAoCalculadoPeloDominio()
    {
        var options = new DbContextOptionsBuilder<LancamentosDbContext>().UseNpgsql(postgres.NovaConnectionString()).Options;
        await using var contexto = new LancamentosDbContext(options);

        var migrator = contexto.GetService<IMigrator>();
        var primeira = contexto.Database.GetMigrations().First();
        await migrator.MigrateAsync(primeira);

        var credito = Guid.NewGuid();
        var debito = Guid.NewGuid();
        var estorno = Guid.NewGuid();
        await InserirAsync(contexto, credito, 1, 1000.50m, null, "k-credito");
        await InserirAsync(contexto, debito, 2, 250.25m, null, "k-debito");
        await InserirAsync(contexto, estorno, 1, 250.25m, debito, "k-estorno");

        await contexto.Database.MigrateAsync();

        var linhas = await contexto.Lancamentos.AsNoTracking().ToDictionaryAsync(l => l.Id);
        Assert.Equal(HashDaRequisicao.DoRegistro(TipoMovimento.Credito, 1000.50m, Quando), linhas[credito].HashRequisicao);
        Assert.Equal(HashDaRequisicao.DoRegistro(TipoMovimento.Debito, 250.25m, Quando), linhas[debito].HashRequisicao);
        Assert.Equal(HashDaRequisicao.DoEstorno(debito), linhas[estorno].HashRequisicao);
        Assert.All(linhas.Values, l => Assert.True(l.CorrespondeAoHash(l.HashRequisicao)));

        var posicao = await contexto.Posicoes.AsNoTracking().SingleAsync(p => p.ContaId == Conta);
        Assert.Equal(1000.50m - 250.25m + 250.25m, posicao.Saldo);
    }

    private static Task<int> InserirAsync(LancamentosDbContext contexto, Guid id, int tipo, decimal valor, Guid? estornoDe, string chave) =>
        contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO lancamentos ("Id", "ContaId", "Tipo", "Valor", "OcorridoEm", "RegistradoEm", "RegistradoPor", "ChaveIdempotencia", "EstornoDeId")
            VALUES ({id}, {Conta}, {(short)tipo}, {valor}, {Quando}, {Quando}, 'legado', {chave}, {estornoDe})
            """);
}
