using Microsoft.AspNetCore.Http;

namespace Bff.API.Sessao;

public sealed class ProtecaoCsrfMiddleware(RequestDelegate next)
{
    private static readonly string[] MetodosSeguros = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ExigeProtecao(context) && !PossuiComprovante(context.Request)
            ? NegarAsync(context)
            : next(context);
    }

    private static bool ExigeProtecao(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && !context.Request.Headers.ContainsKey("Authorization")
        && !MetodosSeguros.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase);

    private static bool PossuiComprovante(HttpRequest request) =>
        request.Headers.ContainsKey(SessaoSchemes.CabecalhoCsrf)
        || string.Equals(request.Headers["Sec-Fetch-Site"], "same-origin", StringComparison.OrdinalIgnoreCase);

    private static Task NegarAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new
        {
            title = "Protecao CSRF",
            status = StatusCodes.Status403Forbidden,
            detail = $"Requisicoes que alteram estado com sessao por cookie exigem o header '{SessaoSchemes.CabecalhoCsrf}'."
        });
    }
}
