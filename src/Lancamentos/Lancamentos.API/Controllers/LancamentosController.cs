using Lancamentos.API.Contracts;
using Lancamentos.Application;
using Lancamentos.Application.Commands;
using Lancamentos.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Verx.SharedKernel;
using Verx.Web.Http;
using Verx.Web.RateLimiting;
using Verx.Web.Security;

namespace Lancamentos.API.Controllers;

[Authorize]
[AutorizaConta]
[ApiController]
[EnableRateLimiting(RateLimitingExtensions.PolicyPadrao)]
[Route("api/contas/{contaId:guid}/lancamentos")]
public class LancamentosController(
    ICommandHandler<RegistrarLancamentoCommand, ResultadoRegistro> registrar,
    ICommandHandler<EstornarLancamentoCommand, ResultadoRegistro> estornar,
    IQueryHandler<ObterLancamentoQuery, LancamentoDto> obter) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<LancamentoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<LancamentoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Registrar(
        Guid contaId, RegistrarLancamentoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await registrar.HandleAsync(
            new RegistrarLancamentoCommand(
                contaId, request.Tipo, request.Valor, request.OcorridoEm,
                IdempotencyKey.Obter(Request), User.Usuario()),
            cancellationToken);

        return Responder(contaId, resultado);
    }

    [HttpPost("{lancamentoId:guid}/estornos")]
    [ProducesResponseType<LancamentoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<LancamentoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Estornar(Guid contaId, Guid lancamentoId, CancellationToken cancellationToken)
    {
        var resultado = await estornar.HandleAsync(
            new EstornarLancamentoCommand(contaId, lancamentoId, IdempotencyKey.Obter(Request), User.Usuario()),
            cancellationToken);

        return Responder(contaId, resultado);
    }

    [HttpGet("{lancamentoId:guid}", Name = nameof(ObterPorId))]
    [ProducesResponseType<LancamentoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorId(Guid contaId, Guid lancamentoId, CancellationToken cancellationToken) =>
        Ok(await obter.HandleAsync(new ObterLancamentoQuery(contaId, lancamentoId), cancellationToken));

    private IActionResult Responder(Guid contaId, ResultadoRegistro resultado)
    {
        if (resultado.Replay)
        {
            Response.Headers["Idempotent-Replayed"] = "true";
            return Ok(resultado.Lancamento);
        }

        return CreatedAtRoute(
            nameof(ObterPorId),
            new { contaId, lancamentoId = resultado.Lancamento.Id },
            resultado.Lancamento);
    }
}
