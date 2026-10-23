using Consolidado.Application;
using Consolidado.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Verx.SharedKernel;
using Verx.Web.RateLimiting;
using Verx.Web.Security;

namespace Consolidado.API.Controllers;

[Authorize]
[AutorizaConta]
[ApiController]
[EnableRateLimiting(RateLimitingExtensions.PolicyPadrao)]
[Route("api/contas/{contaId:guid}")]
public class SaldosController(
    IQueryHandler<ObterSaldoEmQuery, SaldoEmDto> saldoEm,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("saldo")]
    [ProducesResponseType<SaldoEmDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterSaldo(Guid contaId, [FromQuery] DateTimeOffset? em, CancellationToken cancellationToken)
    {
        var instante = em ?? timeProvider.GetUtcNow();
        return Ok(await saldoEm.HandleAsync(new ObterSaldoEmQuery(contaId, instante), cancellationToken));
    }
}
