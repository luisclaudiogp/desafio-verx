using System.Security.Claims;
using Bff.API.Sessao;
using Microsoft.AspNetCore.Http;

namespace Bff.UnitTests;

public class ProtecaoCsrfMiddlewareTests
{
    private static async Task<(bool Passou, int Status)> Executar(
        string metodo, bool autenticado, Action<IHeaderDictionary>? cabecalhos = null)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Method = metodo;
        contexto.Response.Body = new MemoryStream();
        if (autenticado)
            contexto.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u")], "Cookies"));
        cabecalhos?.Invoke(contexto.Request.Headers);

        var passou = false;
        await new ProtecaoCsrfMiddleware(_ =>
        {
            passou = true;
            return Task.CompletedTask;
        }).InvokeAsync(contexto);

        return (passou, contexto.Response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task MetodosSeguros_SempreSeguem(string metodo)
    {
        var (passou, _) = await Executar(metodo, autenticado: true);

        Assert.True(passou);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task EscritaComCookieSemComprovante_E403(string metodo)
    {
        var (passou, status) = await Executar(metodo, autenticado: true);

        Assert.False(passou);
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task EscritaComCookieEHeaderCsrf_Segue()
    {
        var (passou, _) = await Executar("POST", true, h => h[SessaoSchemes.CabecalhoCsrf] = "1");

        Assert.True(passou);
    }

    [Fact]
    public async Task EscritaComCookieEMesmaOrigem_Segue()
    {
        var (passou, _) = await Executar("POST", true, h => h["Sec-Fetch-Site"] = "same-origin");

        Assert.True(passou);
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("none")]
    public async Task EscritaComCookieDeOutraOrigem_E403(string site)
    {
        var (passou, status) = await Executar("POST", true, h => h["Sec-Fetch-Site"] = site);

        Assert.False(passou);
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task EscritaComBearer_NaoSofreCsrf()
    {
        var (passou, _) = await Executar("POST", true, h => h.Authorization = "Bearer x");

        Assert.True(passou);
    }

    [Fact]
    public async Task EscritaAnonima_SegueParaAAutenticacaoDecidir()
    {
        var (passou, _) = await Executar("POST", autenticado: false);

        Assert.True(passou);
    }
}
