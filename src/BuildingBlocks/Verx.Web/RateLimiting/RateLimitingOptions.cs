namespace Verx.Web.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    public int PermitLimitPorUsuario { get; set; } = 100;
    public double ReposicaoPorSegundo { get; set; } = 100;
    public int PermitLimitGlobal { get; set; } = 500;
}
