using System.Security.Claims;
using Bff.API.Auth;
using Bff.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Verx.Web.Security;

namespace Bff.UnitTests;

public class AuthControllerTests
{
    private static readonly TokenResponse Token = new("acc", "ref", 900, 1800, "Bearer");

    private readonly Mock<ITokenProvider> _tokens = new();

    [Fact]
    public async Task Login_DelegaAoProviderEDevolve200()
    {
        _tokens.Setup(t => t.LoginAsync("cliente.a", "s", It.IsAny<CancellationToken>())).ReturnsAsync(Token);

        var resultado = await new AuthController(_tokens.Object)
            .Login(new LoginRequest { Usuario = "cliente.a", Senha = "s" }, CancellationToken.None);

        Assert.Same(Token, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task Renovar_DelegaAoProvider()
    {
        _tokens.Setup(t => t.RenovarAsync("r", It.IsAny<CancellationToken>())).ReturnsAsync(Token);

        var resultado = await new AuthController(_tokens.Object)
            .Renovar(new RefreshRequest { RefreshToken = "r" }, CancellationToken.None);

        Assert.Same(Token, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task Logout_EncerraSessaoEDevolve204()
    {
        var resultado = await new AuthController(_tokens.Object)
            .Logout(new RefreshRequest { RefreshToken = "r" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(resultado);
        _tokens.Verify(t => t.EncerrarAsync("r", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Me_DevolveUsuarioEContasDoToken()
    {
        var conta = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "u1"), new Claim(ContaClaims.ContaId, conta.ToString())], "test"));
        var controller = new AuthController(_tokens.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };

        var sessao = Assert.IsType<SessaoDto>(Assert.IsType<OkObjectResult>(controller.Me()).Value);

        Assert.Equal("u1", sessao.Usuario);
        Assert.Equal([conta], sessao.Contas);
    }
}
