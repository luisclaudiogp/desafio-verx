using System.Security.Claims;
using Bff.API.Gateway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Verx.Web.Security;

namespace Bff.UnitTests;

public class ContaDaRotaAuthorizationHandlerTests
{
    private static async Task<bool> Autorizar(ClaimsPrincipal user, object? valorDaRota, bool recursoHttp = true)
    {
        var http = new DefaultHttpContext();
        if (valorDaRota is not null)
            http.Request.RouteValues[ContaAuthorizationFilter.RouteKey] = valorDaRota;

        var requirement = new ContaDaRotaRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, recursoHttp ? http : null);
        await new ContaDaRotaAuthorizationHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Usuario(params Guid[] contas) =>
        new(new ClaimsIdentity(contas.Select(c => new Claim(ContaClaims.ContaId, c.ToString())), "test"));

    [Fact]
    public async Task ContaDaRotaPertenceAoToken_Autoriza()
    {
        var conta = Guid.NewGuid();
        Assert.True(await Autorizar(Usuario(conta), conta.ToString()));
    }

    [Fact]
    public async Task ContaDeOutroCliente_Nega() =>
        Assert.False(await Autorizar(Usuario(Guid.NewGuid()), Guid.NewGuid().ToString()));

    [Fact]
    public async Task RotaSemConta_Nega() =>
        Assert.False(await Autorizar(Usuario(Guid.NewGuid()), null));

    [Fact]
    public async Task ContaMalformada_Nega() =>
        Assert.False(await Autorizar(Usuario(Guid.NewGuid()), "nao-e-guid"));

    [Fact]
    public async Task RecursoQueNaoEHttpContext_Nega() =>
        Assert.False(await Autorizar(Usuario(Guid.NewGuid()), Guid.NewGuid().ToString(), recursoHttp: false));
}
