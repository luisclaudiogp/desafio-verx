using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Verx.SharedKernel;
using Verx.Web.Http;

namespace Verx.Web.Errors;

public sealed class VerxExceptionHandler(ILogger<VerxExceptionHandler> logger) : IExceptionHandler
{
    private sealed record Mapeamento(Type Tipo, int Status, string Titulo, string? DetalheFixo);

    private static readonly Mapeamento[] Mapeamentos =
    [
        new(typeof(NaoEncontradoException), StatusCodes.Status404NotFound, "Recurso nao encontrado", null),
        new(typeof(DomainException), StatusCodes.Status422UnprocessableEntity, "Regra de negocio violada", null),
        new(typeof(RequisicaoInvalidaException), StatusCodes.Status400BadRequest, "Requisicao invalida", null),
        new(typeof(BadHttpRequestException), StatusCodes.Status400BadRequest, "Requisicao invalida", "Corpo da requisicao invalido."),
        new(typeof(ConcorrenciaException), StatusCodes.Status409Conflict, "Conflito de concorrencia", "Tente novamente.")
    ];

    private static readonly Mapeamento Padrao =
        new(typeof(Exception), StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado.");

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var mapeamento = Mapeamentos.FirstOrDefault(m => m.Tipo.IsInstanceOfType(exception)) ?? Padrao;

        if (mapeamento.Status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Erro nao tratado em {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = mapeamento.Status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = mapeamento.Status,
                Title = mapeamento.Titulo,
                Detail = mapeamento.DetalheFixo ?? exception.Message
            },
            cancellationToken);
        return true;
    }
}
