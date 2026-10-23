using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Moq;
using Verx.Web.Security;

namespace Verx.BuildingBlocks.UnitTests;

public class ContaAuthorizationFilterTests
{
    private readonly Mock<IAuthorizationService> _authorization = new();

    private static ResourceExecutingContext Contexto(object? contaId)
    {
        var http = new DefaultHttpContext();
        var rota = new RouteData();
        if (contaId is not null)
            rota.Values[ContaAuthorizationFilter.RouteKey] = contaId;

        return new ResourceExecutingContext(
            new ActionContext(http, rota, new ActionDescriptor()),
            [],
            []);
    }

    private static (ResourceExecutionDelegate Next, Func<bool> Executou) Proximo()
    {
        var executou = false;
        return (() =>
        {
            executou = true;
            return Task.FromResult<ResourceExecutedContext>(null!);
        }, () => executou);
    }

    [Fact]
    public async Task ContaAutorizada_SegueOPipeline()
    {
        var conta = Guid.NewGuid();
        _authorization.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), conta, ContaClaims.PolicyAcessoConta))
            .ReturnsAsync(AuthorizationResult.Success());
        var contexto = Contexto(conta.ToString());
        var (next, executou) = Proximo();

        await new ContaAuthorizationFilter(_authorization.Object).OnResourceExecutionAsync(contexto, next);

        Assert.True(executou());
        Assert.Null(contexto.Result);
    }

    [Fact]
    public async Task ContaDeOutroCliente_RetornaForbidSemExecutarAcao()
    {
        _authorization.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), ContaClaims.PolicyAcessoConta))
            .ReturnsAsync(AuthorizationResult.Failed());
        var contexto = Contexto(Guid.NewGuid().ToString());
        var (next, executou) = Proximo();

        await new ContaAuthorizationFilter(_authorization.Object).OnResourceExecutionAsync(contexto, next);

        Assert.False(executou());
        Assert.IsType<ForbidResult>(contexto.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nao-e-guid")]
    public async Task ContaAusenteOuMalformada_RetornaForbid(string? valor)
    {
        var contexto = Contexto(valor);
        var (next, executou) = Proximo();

        await new ContaAuthorizationFilter(_authorization.Object).OnResourceExecutionAsync(contexto, next);

        Assert.False(executou());
        Assert.IsType<ForbidResult>(contexto.Result);
    }

    [Fact]
    public void Atributo_UsaOFiltroDeAutorizacao() =>
        Assert.Equal(typeof(ContaAuthorizationFilter), new AutorizaContaAttribute().ImplementationType);
}
