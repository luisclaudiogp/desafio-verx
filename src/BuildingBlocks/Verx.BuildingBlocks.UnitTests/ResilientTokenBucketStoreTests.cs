using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Verx.Web.RateLimiting;
using Verx.Web.Redis;

namespace Verx.BuildingBlocks.UnitTests;

public class ResilientTokenBucketStoreTests
{
    private readonly Mock<ITokenBucketStore> _principal = new();
    private readonly Mock<ITokenBucketStore> _reserva = new();
    private readonly RelogioManual _relogio = new();
    private readonly ResilientTokenBucketStore _sut;

    public ResilientTokenBucketStoreTests()
    {
        _reserva.Setup(r => r.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(true, null));
        _sut = new ResilientTokenBucketStore(
            _principal.Object,
            _reserva.Object,
            ResilientTokenBucketStore.CriarPipelinePadrao(TimeSpan.FromMilliseconds(50), _relogio),
            NullLogger<ResilientTokenBucketStore>.Instance);
    }

    private ValueTask<ResultadoTokenBucket> Consumir(CancellationToken ct = default) =>
        _sut.TentarConsumirAsync("k", 10, 5, ct);

    [Fact]
    public async Task RedisSaudavel_UsaOArmazenamentoDistribuidoSemTocarNaReserva()
    {
        _principal.Setup(p => p.TentarConsumirAsync("k", 10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(false, TimeSpan.FromSeconds(2)));

        var resultado = await Consumir();

        Assert.False(resultado.Permitido);
        Assert.Equal(TimeSpan.FromSeconds(2), resultado.RetryAfter);
        _reserva.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RedisFalha_UsaOLimiteLocalEmVezDeDerrubarARequisicao()
    {
        _principal.Setup(p => p.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis fora"));

        var resultado = await Consumir();

        Assert.True(resultado.Permitido);
        _reserva.Verify(r => r.TentarConsumirAsync("k", 10, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelamentoDoChamador_NaoViraFallback()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _principal.Setup(p => p.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, double, CancellationToken>((_, _, _, ct) => throw new OperationCanceledException(ct));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Consumir(cts.Token));

        _reserva.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FalhasRepetidas_AbremOCircuitoEPoupamORedis()
    {
        _principal.Setup(p => p.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis fora"));

        for (var i = 0; i < 40; i++)
            Assert.True((await Consumir()).Permitido);

        _principal.Verify(
            p => p.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Between(10, 20, Moq.Range.Inclusive));
        _reserva.Verify(
            r => r.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Exactly(40));
    }

    [Fact]
    public async Task CircuitoAberto_FechaAposOPeriodoDePausaERetomaORedis()
    {
        var falhando = true;
        _principal.Setup(p => p.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, double, CancellationToken>((_, _, _, _) =>
                falhando
                    ? throw new InvalidOperationException("redis fora")
                    : ValueTask.FromResult(new ResultadoTokenBucket(false, TimeSpan.FromSeconds(1))));

        for (var i = 0; i < 20; i++)
            await Consumir();
        falhando = false;
        _relogio.Avancar(TimeSpan.FromSeconds(20));

        var depois = await Consumir();

        Assert.False(depois.Permitido);
    }
}
