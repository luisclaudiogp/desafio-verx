using System.Threading.RateLimiting;

namespace Verx.Web.RateLimiting;

public sealed class TokenBucketStoreRateLimiter(
    ITokenBucketStore store,
    string chave,
    int capacidade,
    double reposicaoPorSegundo,
    TimeProvider timeProvider) : RateLimiter
{
    private long _ultimoUso = timeProvider.GetTimestamp();
    private long _esgotadoAte;

    public override TimeSpan? IdleDuration => timeProvider.GetElapsedTime(Interlocked.Read(ref _ultimoUso));

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        if (EsperaConhecida() is { } espera)
            return Negado(espera);

        return permitCount == 0 ? Concedido() : NaoDecididoSemEntradaESaida();
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _ultimoUso, timeProvider.GetTimestamp());

        if (EsperaConhecida() is { } espera)
            return Negado(espera);

        var resultado = await store.TentarConsumirAsync(chave, capacidade, reposicaoPorSegundo, cancellationToken);
        if (resultado is { Permitido: false, RetryAfter: { } retry })
            Interlocked.Exchange(ref _esgotadoAte, timeProvider.GetTimestamp() + ToTimestamp(retry));

        return new StoreRateLimitLease(resultado);
    }

    private TimeSpan? EsperaConhecida()
    {
        var restante = timeProvider.GetElapsedTime(timeProvider.GetTimestamp(), Interlocked.Read(ref _esgotadoAte));
        return restante > TimeSpan.Zero ? restante : null;
    }

    private long ToTimestamp(TimeSpan intervalo) =>
        (long)(intervalo.TotalSeconds * timeProvider.TimestampFrequency);

    private static StoreRateLimitLease Concedido() => new(new ResultadoTokenBucket(true, null));

    private static StoreRateLimitLease Negado(TimeSpan espera) => new(new ResultadoTokenBucket(false, espera));

    private static StoreRateLimitLease NaoDecididoSemEntradaESaida() => new(new ResultadoTokenBucket(false, null));
}
