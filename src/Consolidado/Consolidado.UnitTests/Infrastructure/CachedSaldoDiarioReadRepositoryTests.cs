using System.Text.Json;
using Consolidado.Application;
using Consolidado.Application.Abstractions;
using Consolidado.Infrastructure.Caching;
using Moq;

namespace Consolidado.UnitTests.Infrastructure;

public class CachedSaldoDiarioReadRepositoryTests
{
    private static readonly Guid Conta = Guid.NewGuid();
    private static readonly DateOnly Dia = new(2026, 9, 1);
    private static readonly SaldoDiarioDto Saldo = new(Conta, Dia, 100m, 40m, 60m);

    private readonly Mock<ISaldoDiarioReadRepository> _inner = new();
    private readonly Mock<IFailOpenCache> _cache = new();
    private readonly CachedSaldoDiarioReadRepository _sut;

    public CachedSaldoDiarioReadRepositoryTests()
    {
        _inner.Setup(r => r.ObterSaldoDiarioAsync(Conta, Dia, It.IsAny<CancellationToken>())).ReturnsAsync(Saldo);
        _sut = new CachedSaldoDiarioReadRepository(_inner.Object, _cache.Object);
    }

    [Fact]
    public async Task Hit_RetornaDoCacheSemConsultarOBanco()
    {
        _cache.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(Saldo));

        var resultado = await _sut.ObterSaldoDiarioAsync(Conta, Dia, default);

        Assert.Equal(Saldo, resultado);
        _inner.Verify(r => r.ObterSaldoDiarioAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Miss_ConsultaOBancoEPopulaOCache()
    {
        var resultado = await _sut.ObterSaldoDiarioAsync(Conta, Dia, default);

        Assert.Equal(Saldo, resultado);
        _cache.Verify(c => c.SetStringAsync(
            It.Is<string>(k => k.Contains("2026-09-01", StringComparison.Ordinal)),
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConteudoInvalidoNoCache_TrataComoMiss()
    {
        _cache.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("null");

        Assert.Equal(Saldo, await _sut.ObterSaldoDiarioAsync(Conta, Dia, default));
    }
}
