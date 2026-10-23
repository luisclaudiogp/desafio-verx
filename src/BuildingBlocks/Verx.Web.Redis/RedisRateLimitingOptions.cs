namespace Verx.Web.Redis;

public sealed class RedisRateLimitingOptions
{
    public const string Section = "RateLimiting:Redis";

    public string? ConnectionString { get; set; }
    public int TimeoutMs { get; set; } = 100;

    public bool Habilitado => !string.IsNullOrWhiteSpace(ConnectionString);
}
