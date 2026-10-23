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
public class SaldosDiariosController(
    IQueryHandler<ObterSaldoDiarioQuery, SaldoDiarioDto> saldoDiario) : ControllerBase
{
    [HttpGet("saldo-diario/{data}")]
    [ProducesResponseType<SaldoDiarioDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterSaldoDiario(Guid contaId, DateOnly data, CancellationToken cancellationToken) =>
        Ok(await saldoDiario.HandleAsync(new ObterSaldoDiarioQuery(contaId, data), cancellationToken));
}
