using System.Security.Claims;

namespace Verx.Web.Security;

public static class ContaClaims
{
    public const string ContaId = "conta_id";

    public const string PolicyAcessoConta = "AcessoConta";

    public static string Usuario(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "desconhecido";

    public static IReadOnlyList<Guid> Contas(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return [.. user.FindAll(ContaId)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)];
    }

    public static bool PossuiConta(this ClaimsPrincipal user, Guid contaId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return contaId != Guid.Empty
               && user.FindAll(ContaId).Any(c => Guid.TryParse(c.Value, out var id) && id == contaId);
    }
}
