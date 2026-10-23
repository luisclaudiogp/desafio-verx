using Consolidado.Application;
using Consolidado.Application.Abstractions;
using Consolidado.Application.Commands;
using Consolidado.Infrastructure.Data;
using Consolidado.Infrastructure.Projecao;
using Consolidado.Infrastructure.Repositories;
using Consolidado.Infrastructure.Snapshots;
using Microsoft.EntityFrameworkCore;
using Moq;
using Verx.IntegrationTests.Infra;
using Verx.SharedKernel;

namespace Verx.IntegrationTests;

public abstract class ConsolidadoSnapshotsTestesBase(PostgresFixture postgres, ModoDeProjecao modo)
{
    private static readonly DateTimeOffset Inicio = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Hoje = new(2026, 8, 20, 6, 0, 0, TimeSpan.Zero);

    private Task<Func<ConsolidadoDbContext>> NovoBancoAsync() =>
        postgres.NovoBancoAsync<ConsolidadoDbContext>(o => new ConsolidadoDbContext(o));

    private async Task ProjetarAsync(Func<ConsolidadoDbContext> novo, params MovimentoRecebido[] itens)
    {
        await using var contexto = novo();
        var handler = new RegistrarMovimentosEmLoteHandler(
            new ProjecaoRepository(contexto, new ProjecaoOptions { Modo = modo }), new CorretorDeSnapshots(contexto), Mock.Of<ISaldoCacheInvalidator>(), TimeProvider.System);
        await handler.HandleAsync(new RegistrarMovimentosEmLoteCommand(itens), CancellationToken.None);
    }

    private static async Task<ResultadoSnapshots> GerarAsync(Func<ConsolidadoDbContext> novo, DateTimeOffset agora, int lote = 500)
    {
        await using var contexto = novo();
        return await new GerarSnapshotsHandler(new SnapshotRepository(contexto), new RelogioFixo(agora))
            .HandleAsync(new GerarSnapshotsCommand(1, lote), CancellationToken.None);
    }

    private static MovimentoRecebido Mov(Guid conta, TipoMovimento tipo, decimal valor, DateTimeOffset quando) =>
        new(Guid.NewGuid(), conta, tipo, valor, quando);

    private static IEnumerable<MovimentoRecebido> Historico(Guid conta, int dias, int porDia, int semente)
    {
        var rnd = new Random(semente);
        for (var d = 0; d < dias; d++)
            for (var i = 0; i < porDia; i++)
                yield return Mov(
                    conta,
                    rnd.Next(3) == 0 ? TipoMovimento.Debito : TipoMovimento.Credito,
                    rnd.Next(1, 50000) / 100m,
                    Inicio.AddDays(d).AddMinutes(rnd.Next(0, 1400)));
    }

    private static async Task<(decimal Creditos, decimal Debitos)> SomaBruta(ConsolidadoDbContext contexto, Guid conta, DateTimeOffset ate)
    {
        var movimentos = await contexto.Movimentos.AsNoTracking()
            .Where(m => m.ContaId == conta && m.OcorridoEm <= ate).ToListAsync();
        return (
            movimentos.Where(m => m.Tipo == TipoMovimento.Credito).Sum(m => m.Valor),
            movimentos.Where(m => m.Tipo == TipoMovimento.Debito).Sum(m => m.Valor));
    }

    private static async Task AssertSaldosIguaisASomaBrutaAsync(Func<ConsolidadoDbContext> novo, params Guid[] contas)
    {
        await using var contexto = novo();
        var repo = new SaldoReadRepository(contexto);
        var instantes = Enumerable.Range(-2, 28).SelectMany(d => new[]
        {
            Inicio.AddDays(d),
            Inicio.AddDays(d).AddHours(7).AddMinutes(13),
            Inicio.AddDays(d).AddHours(13).AddSeconds(59)
        });

        foreach (var conta in contas)
            foreach (var t in instantes)
            {
                var esperado = await SomaBruta(contexto, conta, t);
                var obtido = await repo.ObterSaldoEmAsync(conta, t, CancellationToken.None);

                Assert.Equal(esperado.Creditos, obtido.TotalCreditos);
                Assert.Equal(esperado.Debitos, obtido.TotalDebitos);
                Assert.Equal(esperado.Creditos - esperado.Debitos, obtido.Saldo);
            }
    }

    [DockerFact]
    public async Task SaldoEm_ComSnapshots_ESemSnapshots_IgualASomaBrutaEmQualquerInstante()
    {
        var novo = await NovoBancoAsync();
        Guid[] contas = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        await ProjetarAsync(novo, contas.SelectMany((c, i) => Historico(c, 15, 6, i)).ToArray());

        await AssertSaldosIguaisASomaBrutaAsync(novo, contas);

        var gerados = await GerarAsync(novo, Hoje);
        Assert.True(gerados.Gerados >= contas.Length * 14);
        await AssertSaldosIguaisASomaBrutaAsync(novo, contas);
    }

    [DockerFact]
    public async Task GerarSnapshots_EIdempotenteEIncrementalPorDia()
    {
        var novo = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        await ProjetarAsync(novo, Historico(conta, 10, 4, 7).ToArray());

        var primeira = await GerarAsync(novo, Inicio.AddDays(6));
        var repetida = await GerarAsync(novo, Inicio.AddDays(6));
        var seguinte = await GerarAsync(novo, Inicio.AddDays(9));

        Assert.True(primeira.Gerados > 0);
        Assert.Equal(0, repetida.Gerados);
        Assert.True(seguinte.Gerados > 0);
        await using var contexto = novo();
        var snapshots = await contexto.Snapshots.AsNoTracking().OrderBy(s => s.Data).ToListAsync();
        Assert.Equal(snapshots.Count, snapshots.Select(s => s.Data).Distinct().Count());
        Assert.True(snapshots.Max(s => s.Data) <= new DateOnly(2026, 8, 9));
        await AssertSaldosIguaisASomaBrutaAsync(novo, conta);
    }

    [DockerFact]
    public async Task GerarSnapshots_EmLotesPequenos_CobreTodasAsContas()
    {
        var novo = await NovoBancoAsync();
        Guid[] contas = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();
        await ProjetarAsync(novo, contas.SelectMany((c, i) => Historico(c, 5, 3, i)).ToArray());

        var resultado = await GerarAsync(novo, Hoje, lote: 2);

        Assert.True(resultado.Lotes >= 4);
        await using var contexto = novo();
        Assert.Equal(contas.Length, await contexto.Snapshots.Select(s => s.ContaId).Distinct().CountAsync());
        await AssertSaldosIguaisASomaBrutaAsync(novo, contas);
    }

    [DockerFact]
    public async Task EventoAtrasado_CorrigeOsSnapshotsPosterioresNaMesmaTransacao()
    {
        var novo = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        await ProjetarAsync(novo, Historico(conta, 12, 4, 3).ToArray());
        await GerarAsync(novo, Hoje);

        await ProjetarAsync(
            novo,
            Mov(conta, TipoMovimento.Credito, 1234.56m, Inicio.AddDays(2).AddHours(1)),
            Mov(conta, TipoMovimento.Debito, 10.10m, Inicio.AddDays(5).AddHours(2)),
            Mov(conta, TipoMovimento.Credito, 99.99m, Inicio.AddDays(-5)));

        await AssertSaldosIguaisASomaBrutaAsync(novo, conta);
        await using var contexto = novo();
        var ultimo = await contexto.Snapshots.AsNoTracking().OrderByDescending(s => s.Data).FirstAsync();
        var bruto = await SomaBruta(contexto, conta, new DateTimeOffset(ultimo.Data.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero));
        Assert.Equal(bruto.Creditos, ultimo.TotalCreditos);
        Assert.Equal(bruto.Debitos, ultimo.TotalDebitos);
    }

    [DockerFact]
    public async Task GeradorConcorrenteComEventosAtrasados_MantemSaldoExato()
    {
        var novo = await NovoBancoAsync();
        Guid[] contas = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        await ProjetarAsync(novo, contas.SelectMany((c, i) => Historico(c, 12, 5, i)).ToArray());
        await GerarAsync(novo, Inicio.AddDays(4));

        var rnd = new Random(11);
        var lotes = Enumerable.Range(0, 12)
            .Select(_ => contas.SelectMany(c => Enumerable.Range(0, 3).Select(_ => Mov(
                c,
                rnd.Next(2) == 0 ? TipoMovimento.Credito : TipoMovimento.Debito,
                rnd.Next(1, 9000) / 100m,
                Inicio.AddDays(rnd.Next(0, 12)).AddMinutes(rnd.Next(0, 1400)))).ToArray()).ToArray())
            .ToList();

        var atrasados = lotes.Select(l => Task.Run(() => ProjetarAsync(novo, l))).Cast<Task>();
        var geradores = Enumerable.Range(0, 6).Select(i => Task.Run(() => GerarAsync(novo, Inicio.AddDays(6 + i), lote: 2))).Cast<Task>();

        await Task.WhenAll(atrasados.Concat(geradores));
        await GerarAsync(novo, Hoje);

        await AssertSaldosIguaisASomaBrutaAsync(novo, contas);
    }
}
