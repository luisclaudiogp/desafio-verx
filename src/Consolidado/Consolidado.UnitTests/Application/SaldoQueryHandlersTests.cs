using Consolidado.Application;
using Consolidado.Application.Abstractions;
using Consolidado.Application.Queries;
using Moq;

namespace Consolidado.UnitTests.Application;

public class SaldoQueryHandlersTests
{
    [Fact]
    public async Task SaldoDiario_DelegaAoRepositorio()
    {
        var conta = Guid.NewGuid();
        var data = new DateOnly(2026, 9, 1);
        var esperado = new SaldoDiarioDto(conta, data, 10m, 3m, 7m);
        var repository = new Mock<ISaldoDiarioReadRepository>();
        repository.Setup(r => r.ObterSaldoDiarioAsync(conta, data, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var dto = await new ObterSaldoDiarioHandler(repository.Object)
            .HandleAsync(new ObterSaldoDiarioQuery(conta, data), CancellationToken.None);

        Assert.Equal(esperado, dto);
    }

    [Fact]
    public async Task SaldoEm_DelegaAoRepositorio()
    {
        var conta = Guid.NewGuid();
        var em = DateTimeOffset.UtcNow;
        var esperado = new SaldoEmDto(conta, em, 10m, 3m, 7m);
        var repository = new Mock<ISaldoEmReadRepository>();
        repository.Setup(r => r.ObterSaldoEmAsync(conta, em, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var dto = await new ObterSaldoEmHandler(repository.Object)
            .HandleAsync(new ObterSaldoEmQuery(conta, em), CancellationToken.None);

        Assert.Equal(esperado, dto);
    }
}
