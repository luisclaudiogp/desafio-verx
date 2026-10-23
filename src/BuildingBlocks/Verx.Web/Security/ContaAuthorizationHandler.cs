using Microsoft.AspNetCore.Authorization;

namespace Verx.Web.Security;

public sealed class ContaAuthorizationHandler : AuthorizationHandler<ContaRequirement, Guid>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ContaRequirement requirement, Guid contaId)
    {
        if (context.User.PossuiConta(contaId))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
