using Bff.API.Sessao;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bff.API.Controllers;

[ApiController]
[Route("bff")]
public class SessaoController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl) =>
        Challenge(
            new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/" },
            SessaoSchemes.OpenId);

    [Authorize(AuthenticationSchemes = SessaoSchemes.Cookie)]
    [HttpPost("logout")]
    public IActionResult Logout() =>
        SignOut(new AuthenticationProperties { RedirectUri = "/" }, SessaoSchemes.Cookie, SessaoSchemes.OpenId);
}
