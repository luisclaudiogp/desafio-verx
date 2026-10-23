namespace Consolidado.Infrastructure.Caching;

public interface IFailOpenCache
{
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken);

    Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
