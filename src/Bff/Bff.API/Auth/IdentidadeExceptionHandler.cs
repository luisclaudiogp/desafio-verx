using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bff.API.Auth;

public sealed class IdentidadeExceptionHandler(ILogger<IdentidadeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var problema = exception switch
        {
            CredenciaisInvalidasException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Nao autenticado",
                Detail = exception.Message
            },
            IdentidadeIndisponivelException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Servico de identidade indisponivel",
                Detail = "Tente novamente em instantes."
            },
            _ => null
        };

        if (problema is null)
            return false;

        if (problema.Status == StatusCodes.Status503ServiceUnavailable)
            logger.LogWarning(exception, "Falha ao falar com o servico de identidade");

        httpContext.Response.StatusCode = problema.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problema, cancellationToken);
        return true;
    }
}
