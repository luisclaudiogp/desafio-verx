using Microsoft.AspNetCore.Mvc;

namespace Verx.Web.Security;

public sealed class AutorizaContaAttribute() : TypeFilterAttribute(typeof(ContaAuthorizationFilter));
