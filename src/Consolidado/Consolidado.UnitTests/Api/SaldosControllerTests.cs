using Consolidado.API.Controllers;
using Consolidado.Application;
using Consolidado.Application.Queries;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Verx.SharedKernel;

namespace Consolidado.UnitTests.Api;

public class SaldosControllerTests
{
    private static readonly Guid Conta = Guid.NewGuid();
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IQueryHandler<ObterSaldoDiarioQuery, SaldoDiarioDto>> _diario = new();
    private readonly Mock<IQueryHandler<ObterSaldoEmQuery, SaldoEmDto>> _em = new();
    private readonly SaldosController _controller;

    public SaldosControllerTests()
    {
        _controller = new SaldosController(_diario.Object, _em.Object, new Fixo(Agora));
    }

    [Fact]
    public async Task SaldoDiario_RetornaDto()
    {
        var dto = new SaldoDiarioDto(Conta, new DateOnly(2026, 9, 1), 10m, 2m, 8m);
        _diario.Setup(h => h.HandleAsync(It.IsAny<ObterSaldoDiarioQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = Assert.IsType<OkObjectResult>(await _controller.ObterSaldoDiario(Conta, new DateOnly(2026, 9, 1), default));

        Assert.Equal(dto, result.Value);
    }

    [Fact]
    public async Task Saldo_SemInstante_UsaAgora()
    {
        _em.Setup(h => h.HandleAsync(It.IsAny<ObterSaldoEmQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaldoEmDto(Conta, Agora, 1m, 0m, 1m));

        await _controller.ObterSaldo(Conta, null, default);

        _em.Verify(h => h.HandleAsync(new ObterSaldoEmQuery(Conta, Agora), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Saldo_ComInstante_UsaInstanteInformado()
    {
        var instante = Agora.AddDays(-10);
        _em.Setup(h => h.HandleAsync(It.IsAny<ObterSaldoEmQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaldoEmDto(Conta, instante, 1m, 0m, 1m));

        await _controller.ObterSaldo(Conta, instante, default);

        _em.Verify(h => h.HandleAsync(new ObterSaldoEmQuery(Conta, instante), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
