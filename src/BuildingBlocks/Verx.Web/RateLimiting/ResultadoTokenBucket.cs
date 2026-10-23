namespace Verx.Web.RateLimiting;

public readonly record struct ResultadoTokenBucket(bool Permitido, TimeSpan? RetryAfter);
