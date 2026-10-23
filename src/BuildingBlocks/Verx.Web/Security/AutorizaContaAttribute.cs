using Microsoft.AspNetCore.Mvc;

namespace Verx.Web.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AutorizaContaAttribute() : TypeFilterAttribute(typeof(ContaAuthorizationFilter));
