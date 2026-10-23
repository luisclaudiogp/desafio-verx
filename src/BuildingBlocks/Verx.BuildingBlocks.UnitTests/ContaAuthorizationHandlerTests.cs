using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Verx.Web.Security;

namespace Verx.BuildingBlocks.UnitTests;

public class ContaAuthorizationHandlerTests
{
    private static async Task<bool> Autorizar(ClaimsPrincipal user, Guid conta)
    {
        var requirement = new ContaRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, conta);
        await new ContaAuthorizationHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Usuario(params string[] contas) =>
        new(new ClaimsIdentity(contas.Select(c => new Claim(ContaClaims.ContaId, c)), "test"));

    [Fact]
    public async Task ContaPropria_Autoriza()
    {
        var conta = Guid.NewGuid();
        Assert.True(await Autorizar(Usuario(conta.ToString()), conta));
    }

    [Fact]
    public async Task UmaDentreVariasContas_Autoriza()
    {
        var conta = Guid.NewGuid();
        Assert.True(await Autorizar(Usuario(Guid.NewGuid().ToString(), conta.ToString()), conta));
    }

    [Fact]
    public async Task ContaDeOutroCliente_Nega() =>
        Assert.False(await Autorizar(Usuario(Guid.NewGuid().ToString()), Guid.NewGuid()));

    [Fact]
    public async Task ClaimMalformada_Nega() =>
        Assert.False(await Autorizar(Usuario("nao-e-guid"), Guid.NewGuid()));

    [Fact]
    public async Task SemClaim_Nega() =>
        Assert.False(await Autorizar(Usuario(), Guid.NewGuid()));

    [Fact]
    public void Usuario_PrefereSub()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "abc"), new Claim(ClaimTypes.NameIdentifier, "xyz")], "test"));
        Assert.Equal("abc", user.Usuario());
    }

    [Fact]
    public void Usuario_SemIdentificacao_RetornaDesconhecido() =>
        Assert.Equal("desconhecido", new ClaimsPrincipal(new ClaimsIdentity()).Usuario());
}
