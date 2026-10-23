using System.Net.Http.Headers;
using Bff.API.Sessao;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Yarp.ReverseProxy.Transforms;

namespace Bff.UnitTests;

public class TokenDaSessaoRequestTransformTests
{
    private static RequestTransformContext Contexto(string? tokenDaSessao, string? authorizationRecebido = null)
    {
        var autenticacao = new Mock<IAuthenticationService>();
        var propriedades = new AuthenticationProperties();
        if (tokenDaSessao is not null)
            propriedades.StoreTokens([new AuthenticationToken { Name = "access_token", Value = tokenDaSessao }]);
        autenticacao.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), SessaoSchemes.Cookie))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(), propriedades, SessaoSchemes.Cookie)));

        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton(autenticacao.Object).BuildServiceProvider()
        };
        if (authorizationRecebido is not null)
            http.Request.Headers.Authorization = authorizationRecebido;

        return new RequestTransformContext { HttpContext = http, ProxyRequest = new HttpRequestMessage() };
    }

    [Fact]
    public async Task SessaoPorCookie_AnexaOBearerDoServidor()
    {
        var contexto = Contexto("token-da-sessao");

        await new TokenDaSessaoRequestTransform().ApplyAsync(contexto);

        Assert.Equal(new AuthenticationHeaderValue("Bearer", "token-da-sessao"), contexto.ProxyRequest.Headers.Authorization);
    }

    [Fact]
    public async Task ClienteJaEnviouBearer_NaoSubstitui()
    {
        var contexto = Contexto("token-da-sessao", authorizationRecebido: "Bearer do-cliente");
        contexto.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer do-cliente");

        await new TokenDaSessaoRequestTransform().ApplyAsync(contexto);

        Assert.Equal("do-cliente", contexto.ProxyRequest.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task SemTokenNaSessao_NaoAnexaNada()
    {
        var contexto = Contexto(null);

        await new TokenDaSessaoRequestTransform().ApplyAsync(contexto);

        Assert.Null(contexto.ProxyRequest.Headers.Authorization);
    }
}
