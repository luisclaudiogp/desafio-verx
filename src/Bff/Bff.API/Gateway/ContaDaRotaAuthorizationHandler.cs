using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Verx.Web.Security;

namespace Bff.API.Gateway;

public sealed class ContaDaRotaAuthorizationHandler : AuthorizationHandler<ContaDaRotaRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ContaDaRotaRequirement requirement)
    {
        if (context.Resource is HttpContext http
            && Guid.TryParse(http.GetRouteValue(ContaAuthorizationFilter.RouteKey)?.ToString(), out var contaId)
            && context.User.PossuiConta(contaId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
