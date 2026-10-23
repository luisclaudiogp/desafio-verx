using System.Security.Claims;
using Verx.Web.Security;

namespace Verx.BuildingBlocks.UnitTests;

public class ContaClaimsTests
{
    private static ClaimsPrincipal Usuario(params string[] contas) =>
        new(new ClaimsIdentity(contas.Select(c => new Claim(ContaClaims.ContaId, c)), "test"));

    [Fact]
    public void Contas_ListaApenasGuidsValidos()
    {
        var conta = Guid.NewGuid();

        Assert.Equal([conta], Usuario(conta.ToString(), "lixo", Guid.Empty.ToString()).Contas());
    }

    [Fact]
    public void PossuiConta_GuidVazio_Nega() =>
        Assert.False(Usuario(Guid.Empty.ToString()).PossuiConta(Guid.Empty));

    [Fact]
    public void PossuiConta_ContaDoToken_Afirma()
    {
        var conta = Guid.NewGuid();

        Assert.True(Usuario(conta.ToString()).PossuiConta(conta));
    }
}
