using Consolidado.Application;
using Consolidado.Application.Abstractions;
using Consolidado.Application.Commands;
using Consolidado.Application.Queries;
using Consolidado.Infrastructure.Data;
using Consolidado.Infrastructure.Projecao;
using Consolidado.Infrastructure.Repositories;
using Consolidado.Infrastructure.Snapshots;
using Microsoft.EntityFrameworkCore;
using Moq;
using Verx.IntegrationTests.Infra;
using Verx.SharedKernel;

namespace Verx.IntegrationTests;

public abstract class ConsolidadoProjecaoTestesBase(PostgresFixture postgres, ModoDeProjecao modo)
{
    private static readonly DateTimeOffset Meio = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private Task<Func<ConsolidadoDbContext>> NovoBancoAsync() =>
        postgres.NovoBancoAsync<ConsolidadoDbContext>(o => new ConsolidadoDbContext(o));

    private async Task<ResultadoProjecao> ProcessarAsync(
        Func<ConsolidadoDbContext> novoContexto, params MovimentoRecebido[] itens)
    {
        await using var contexto = novoContexto();
        var handler = new RegistrarMovimentosEmLoteHandler(
            new ProjecaoRepository(contexto, new ProjecaoOptions { Modo = modo }), new CorretorDeSnapshots(contexto), Mock.Of<ISaldoCacheInvalidator>(), TimeProvider.System);
        return await handler.HandleAsync(new RegistrarMovimentosEmLoteCommand(itens), CancellationToken.None);
    }

    private static MovimentoRecebido Item(Guid conta, TipoMovimento tipo, decimal valor, DateTimeOffset? quando = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), conta, tipo, valor, quando ?? Meio);

    [DockerFact]
    public async Task LotesConcorrentesSobreAsMesmasContas_SomamExatamenteSemPerdaNemDeadlock()
    {
        var novoContexto = await NovoBancoAsync();
        var contas = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var lotes = Enumerable.Range(0, 30)
            .Select(_ => contas
                .SelectMany(c => new[]
                {
                    Item(c, TipoMovimento.Credito, 10m),
                    Item(c, TipoMovimento.Credito, 5m),
                    Item(c, TipoMovimento.Debito, 3m)
                })
                .OrderBy(_ => Random.Shared.Next())
                .ToArray())
            .ToList();

        var resultados = await Task.WhenAll(lotes.Select(l => Task.Run(() => ProcessarAsync(novoContexto, l))));

        Assert.All(resultados, r => Assert.Equal(30, r.Aplicados));
        await using var leitura = novoContexto();
        foreach (var conta in contas)
        {
            var saldo = await new SaldoReadRepository(leitura).ObterSaldoDiarioAsync(conta, new DateOnly(2026, 9, 1), default);
            Assert.Equal(30 * 15m, saldo.TotalCreditos);
            Assert.Equal(30 * 3m, saldo.TotalDebitos);
        }

        Assert.Equal(30 * 30, await leitura.Movimentos.CountAsync());
    }

    [DockerFact]
    public async Task MesmoLoteEntregueEmParalelo_ESomadoUmaUnicaVez()
    {
        var novoContexto = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        var lote = Enumerable.Range(0, 100).Select(_ => Item(conta, TipoMovimento.Credito, 1m)).ToArray();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => ProcessarAsync(novoContexto, lote))));

        Assert.Equal(100, resultados.Sum(r => r.Aplicados));
        Assert.Equal(700, resultados.Sum(r => r.Duplicados));
        await using var leitura = novoContexto();
        var saldo = await new SaldoReadRepository(leitura).ObterSaldoDiarioAsync(conta, new DateOnly(2026, 9, 1), default);
        Assert.Equal(100m, saldo.TotalCreditos);
    }

    [DockerFact]
    public async Task EventoInvalido_FicaEmQuarentenaSemBloquearOsDemais()
    {
        var novoContexto = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        var invalido = Item(conta, TipoMovimento.Credito, -1m);

        var resultado = await ProcessarAsync(novoContexto, invalido, Item(conta, TipoMovimento.Credito, 10m));

        Assert.Equal(new ResultadoProjecao(1, 0, 1), resultado);
        await using var leitura = novoContexto();
        var rejeitado = await leitura.EventosRejeitados.SingleAsync();
        Assert.Equal(invalido.LancamentoId, rejeitado.LancamentoId);
        Assert.Contains("maior que zero", rejeitado.Motivo, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task SaldoEmUmInstante_ConsideraApenasMovimentosAteOCorte()
    {
        var novoContexto = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        await ProcessarAsync(novoContexto,
            Item(conta, TipoMovimento.Credito, 100m, Meio.AddDays(-2)),
            Item(conta, TipoMovimento.Debito, 30m, Meio.AddHours(-1)),
            Item(conta, TipoMovimento.Credito, 500m, Meio.AddHours(1)),
            Item(Guid.NewGuid(), TipoMovimento.Credito, 999m, Meio.AddDays(-2)));

        await using var leitura = novoContexto();
        var handler = new ObterSaldoEmHandler(new SaldoReadRepository(leitura));

        Assert.Equal(70m, (await handler.HandleAsync(new ObterSaldoEmQuery(conta, Meio), default)).Saldo);
        Assert.Equal(570m, (await handler.HandleAsync(new ObterSaldoEmQuery(conta, Meio.AddDays(1)), default)).Saldo);
        Assert.Equal(0m, (await handler.HandleAsync(new ObterSaldoEmQuery(conta, Meio.AddDays(-10)), default)).Saldo);
    }

    [DockerFact]
    public async Task SaldoDiario_SemMovimentos_RetornaZerado()
    {
        var novoContexto = await NovoBancoAsync();
        await using var leitura = novoContexto();

        var saldo = await new SaldoReadRepository(leitura)
            .ObterSaldoDiarioAsync(Guid.NewGuid(), new DateOnly(2026, 9, 1), default);

        Assert.Equal(0m, saldo.Saldo);
    }
}
