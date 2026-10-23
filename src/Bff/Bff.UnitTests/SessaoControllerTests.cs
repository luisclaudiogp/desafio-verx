using Bff.API.Controllers;
using Bff.API.Sessao;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Bff.UnitTests;

public class SessaoControllerTests
{
    private static SessaoController Controller(bool urlLocal)
    {
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string?>())).Returns(urlLocal);
        return new SessaoController { Url = url.Object };
    }

    [Fact]
    public void Login_DesafiaOOpenIdConnectComARotaLocalInformada()
    {
        var resultado = Assert.IsType<ChallengeResult>(Controller(urlLocal: true).Login("/auth/me"));

        Assert.Equal([SessaoSchemes.OpenId], resultado.AuthenticationSchemes);
        Assert.Equal("/auth/me", resultado.Properties!.RedirectUri);
    }

    [Theory]
    [InlineData("https://malicioso.example/roubar")]
    [InlineData(null)]
    public void Login_RotaNaoLocal_VoltaParaARaiz(string? destino)
    {
        var resultado = Assert.IsType<ChallengeResult>(Controller(urlLocal: false).Login(destino));

        Assert.Equal("/", resultado.Properties!.RedirectUri);
    }

    [Fact]
    public void Logout_EncerraASessaoLocalENoIdp()
    {
        var resultado = Assert.IsType<SignOutResult>(Controller(urlLocal: true).Logout());

        Assert.Equal([SessaoSchemes.Cookie, SessaoSchemes.OpenId], resultado.AuthenticationSchemes);
        Assert.Equal("/", resultado.Properties!.RedirectUri);
    }
}
