using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

using Verx.Web.RateLimiting;

namespace Verx.Web.Redis;

public sealed class ResilientTokenBucketStore : ITokenBucketStore
{
    private readonly ITokenBucketStore _principal;
    private readonly ITokenBucketStore _reserva;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ResilientTokenBucketStore> _logger;

    public ResilientTokenBucketStore(
        ITokenBucketStore principal,
        ITokenBucketStore reserva,
        ResiliencePipeline pipeline,
        ILogger<ResilientTokenBucketStore> logger)
    {
        _principal = principal;
        _reserva = reserva;
        _pipeline = pipeline;
        _logger = logger;
    }

    public static ResiliencePipeline CriarPipelinePadrao(TimeSpan timeout, TimeProvider timeProvider) =>
        new ResiliencePipelineBuilder { TimeProvider = timeProvider }
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(15)
            })
            .AddTimeout(new TimeoutStrategyOptions { Timeout = timeout })
            .Build();

    public async ValueTask<ResultadoTokenBucket> TentarConsumirAsync(
        string chave, int capacidade, double reposicaoPorSegundo, CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await _principal.TentarConsumirAsync(chave, capacidade, reposicaoPorSegundo, token),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex is not BrokenCircuitException)
                _logger.LogWarning(ex, "Rate limit distribuido indisponivel; usando o limite local desta instancia.");

            return await _reserva.TentarConsumirAsync(chave, capacidade, reposicaoPorSegundo, cancellationToken);
        }
    }
}
