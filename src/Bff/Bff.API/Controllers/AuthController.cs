using Bff.API.Auth;
using Bff.API.Gateway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Verx.Web.Security;

namespace Bff.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(ITokenProvider tokens) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(GatewayPolicies.Login)]
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await tokens.LoginAsync(request.Usuario, request.Senha, cancellationToken));

    [AllowAnonymous]
    [EnableRateLimiting(GatewayPolicies.Login)]
    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Renovar(RefreshRequest request, CancellationToken cancellationToken) =>
        Ok(await tokens.RenovarAsync(request.RefreshToken, cancellationToken));

    [AllowAnonymous]
    [EnableRateLimiting(GatewayPolicies.Login)]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await tokens.EncerrarAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<SessaoDto>(StatusCodes.Status200OK)]
    public IActionResult Me() => Ok(new SessaoDto(User.Usuario(), User.Contas()));
}
