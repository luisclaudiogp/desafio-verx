using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Verx.Web.Security;

public sealed class ContaAuthorizationFilter(IAuthorizationService authorization) : IAsyncResourceFilter
{
    public const string RouteKey = "contaId";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var valor = context.RouteData.Values[RouteKey]?.ToString();
        if (!Guid.TryParse(valor, out var contaId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var resultado = await authorization.AuthorizeAsync(context.HttpContext.User, contaId, ContaClaims.PolicyAcessoConta);
        if (!resultado.Succeeded)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
