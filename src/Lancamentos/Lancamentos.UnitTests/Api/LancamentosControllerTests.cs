using System.Security.Claims;
using Lancamentos.API.Contracts;
using Lancamentos.API.Controllers;
using Lancamentos.Application;
using Lancamentos.Application.Commands;
using Lancamentos.Application.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Verx.SharedKernel;
using Verx.Web.Http;

namespace Lancamentos.UnitTests.Api;

public class LancamentosControllerTests
{
    private static readonly Guid Conta = Guid.NewGuid();

    private readonly Mock<ICommandHandler<RegistrarLancamentoCommand, ResultadoRegistro>> _registrar = new();
    private readonly Mock<ICommandHandler<EstornarLancamentoCommand, ResultadoRegistro>> _estornar = new();
    private readonly Mock<IQueryHandler<ObterLancamentoQuery, LancamentoDto>> _obter = new();
    private readonly LancamentosController _controller;

    public LancamentosControllerTests()
    {
        _controller = new LancamentosController(_registrar.Object, _estornar.Object, _obter.Object);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "test"))
        };
        http.Request.Headers["Idempotency-Key"] = "chave-1";
        _controller.ControllerContext = new ControllerContext { HttpContext = http };
    }

    private static LancamentoDto Dto() =>
        new(Guid.NewGuid(), Conta, TipoMovimento.Credito, 10m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

    private static RegistrarLancamentoRequest Request() => new(TipoMovimento.Credito, 10m, null);

    [Fact]
    public async Task Registrar_Novo_Retorna201ComComandoCorreto()
    {
        _registrar.Setup(h => h.HandleAsync(It.IsAny<RegistrarLancamentoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoRegistro(Dto(), false));

        var result = await _controller.Registrar(Conta, Request(), default);

        Assert.IsType<CreatedAtRouteResult>(result);
        _registrar.Verify(h => h.HandleAsync(
            It.Is<RegistrarLancamentoCommand>(c => c.ChaveIdempotencia == "chave-1" && c.Usuario == "user-1" && c.ContaId == Conta),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Registrar_Replay_Retorna200ComHeader()
    {
        _registrar.Setup(h => h.HandleAsync(It.IsAny<RegistrarLancamentoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoRegistro(Dto(), true));

        var result = await _controller.Registrar(Conta, Request(), default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("true", _controller.Response.Headers["Idempotent-Replayed"].ToString());
    }

    [Fact]
    public async Task Registrar_SemHeaderDeIdempotencia_LancaRequisicaoInvalida()
    {
        _controller.Request.Headers.Remove("Idempotency-Key");

        await Assert.ThrowsAsync<RequisicaoInvalidaException>(() => _controller.Registrar(Conta, Request(), default));
    }

    [Fact]
    public async Task Estornar_Novo_Retorna201()
    {
        _estornar.Setup(h => h.HandleAsync(It.IsAny<EstornarLancamentoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoRegistro(Dto(), false));

        Assert.IsType<CreatedAtRouteResult>(await _controller.Estornar(Conta, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Estornar_Replay_Retorna200()
    {
        _estornar.Setup(h => h.HandleAsync(It.IsAny<EstornarLancamentoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoRegistro(Dto(), true));

        Assert.IsType<OkObjectResult>(await _controller.Estornar(Conta, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task ObterPorId_Retorna200()
    {
        _obter.Setup(h => h.HandleAsync(It.IsAny<ObterLancamentoQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(Dto());

        Assert.IsType<OkObjectResult>(await _controller.ObterPorId(Conta, Guid.NewGuid(), default));
    }
}
