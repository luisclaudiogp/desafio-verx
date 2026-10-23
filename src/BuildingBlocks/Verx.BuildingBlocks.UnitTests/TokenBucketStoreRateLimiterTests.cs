using System.Threading.RateLimiting;
using Moq;
using Verx.Web.RateLimiting;

namespace Verx.BuildingBlocks.UnitTests;

public class TokenBucketStoreRateLimiterTests
{
    private readonly RelogioManual _relogio = new();
    private readonly Mock<ITokenBucketStore> _store = new();

    private TokenBucketStoreRateLimiter Criar() => new(_store.Object, "minha-chave", 10, 5, _relogio);

    [Fact]
    public async Task Permitido_DevolveLeaseAdquirido()
    {
        _store.Setup(s => s.TentarConsumirAsync("minha-chave", 10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(true, null));

        using var lease = await Criar().AcquireAsync(1);

        Assert.True(lease.IsAcquired);
        Assert.False(lease.TryGetMetadata(MetadataName.RetryAfter, out _));
    }

    [Fact]
    public async Task Negado_DevolveRetryAfterNosMetadados()
    {
        _store.Setup(s => s.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(false, TimeSpan.FromMilliseconds(750)));

        using var lease = await Criar().AcquireAsync(1);

        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var espera));
        Assert.Equal(TimeSpan.FromMilliseconds(750), espera);
        Assert.Contains(MetadataName.RetryAfter.Name, lease.MetadataNames);
    }

    [Fact]
    public async Task LeaseNegado_NaoExpoeMetadadoDesconhecido()
    {
        _store.Setup(s => s.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(false, TimeSpan.FromSeconds(1)));

        using var lease = await Criar().AcquireAsync(1);

        Assert.False(lease.TryGetMetadata("OUTRO", out var valor));
        Assert.Null(valor);
    }

    [Fact]
    public async Task IdleDuration_CrescePeloRelogioEZeraAoUsar()
    {
        _store.Setup(s => s.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(true, null));
        using var limiter = Criar();

        _relogio.Avancar(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(30), limiter.IdleDuration);

        using var _ = await limiter.AcquireAsync(1);
        Assert.Equal(TimeSpan.Zero, limiter.IdleDuration);
    }

    [Fact]
    public void AcquireSincrono_SemInformacao_NaoDecideENaoVaiAoArmazenamento()
    {
        using var limiter = Criar();

        using var lease = limiter.AttemptAcquire(1);

        Assert.False(lease.IsAcquired);
        Assert.False(lease.TryGetMetadata(MetadataName.RetryAfter, out _));
        _store.VerifyNoOtherCalls();
    }

    [Fact]
    public void AcquireSincrono_ComZeroPermits_InformaQueAindaNaoEstaEsgotado()
    {
        using var limiter = Criar();

        using var lease = limiter.AttemptAcquire(0);

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public async Task AposNegativa_OLimitadorLembraAEsperaEPoupaOArmazenamento()
    {
        _store.Setup(s => s.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(false, TimeSpan.FromSeconds(2)));
        using var limiter = Criar();
        using var primeira = await limiter.AcquireAsync(1);

        _relogio.Avancar(TimeSpan.FromMilliseconds(500));
        using var sincrona = limiter.AttemptAcquire(1);
        using var assincrona = await limiter.AcquireAsync(1);

        Assert.False(sincrona.IsAcquired);
        Assert.True(sincrona.TryGetMetadata(MetadataName.RetryAfter, out var espera));
        Assert.Equal(TimeSpan.FromMilliseconds(1500), espera);
        Assert.False(assincrona.IsAcquired);
        _store.Verify(
            s => s.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PassadaAEsperaConhecida_OArmazenamentoVoltaASerConsultado()
    {
        _store.SetupSequence(s => s.TentarConsumirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoTokenBucket(false, TimeSpan.FromSeconds(1)))
            .ReturnsAsync(new ResultadoTokenBucket(true, null));
        using var limiter = Criar();
        using var negado = await limiter.AcquireAsync(1);

        _relogio.Avancar(TimeSpan.FromSeconds(2));
        using var liberado = await limiter.AcquireAsync(1);

        Assert.False(negado.IsAcquired);
        Assert.True(liberado.IsAcquired);
    }

    [Fact]
    public void Estatisticas_NaoSaoExpostas()
    {
        using var limiter = Criar();

        Assert.Null(limiter.GetStatistics());
    }
}
