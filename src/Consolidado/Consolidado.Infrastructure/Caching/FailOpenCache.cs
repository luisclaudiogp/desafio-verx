using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Consolidado.Infrastructure.Caching;

public sealed class FailOpenCache(
    IDistributedCache cache,
    ResiliencePipeline pipeline,
    ILogger<FailOpenCache> logger) : IFailOpenCache
{
    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken) =>
        ExecutarAsync(ct => cache.GetStringAsync(key, ct), null, "leitura", cancellationToken);

    public Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var opcoes = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        return ExecutarAsync(async ct =>
        {
            await cache.SetStringAsync(key, value, opcoes, ct);
            return true;
        }, false, "gravacao", cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken) =>
        ExecutarAsync(async ct =>
        {
            await cache.RemoveAsync(key, ct);
            return true;
        }, false, "invalidacao", cancellationToken);

    public static ResiliencePipeline CriarPipelinePadrao() =>
        new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(15)
            })
            .AddTimeout(TimeSpan.FromMilliseconds(250))
            .Build();

    private async Task<T> ExecutarAsync<T>(
        Func<CancellationToken, Task<T>> operacao, T valorSeFalhar, string nome, CancellationToken cancellationToken)
    {
        try
        {
            return await pipeline.ExecuteAsync(async ct => await operacao(ct), cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cache indisponivel na {Operacao}; seguindo sem cache.", nome);
            return valorSeFalhar;
        }
    }
}
