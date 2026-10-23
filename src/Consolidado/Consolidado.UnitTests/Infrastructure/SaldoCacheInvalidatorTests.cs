using Consolidado.Infrastructure.Caching;
using Moq;

namespace Consolidado.UnitTests.Infrastructure;

public class SaldoCacheInvalidatorTests
{
    [Fact]
    public async Task Invalidar_RemoveAChaveDoDia()
    {
        var cache = new Mock<IFailOpenCache>();
        var conta = Guid.NewGuid();

        await new SaldoCacheInvalidator(cache.Object).InvalidarSaldoDiarioAsync(conta, new DateOnly(2026, 9, 1), default);

        cache.Verify(c => c.RemoveAsync(
            It.Is<string>(k => k.Contains(conta.ToString("N"), StringComparison.Ordinal) && k.EndsWith("2026-09-01", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
