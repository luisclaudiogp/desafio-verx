namespace Verx.Web.RateLimiting;

public interface ITokenBucketStore
{
    ValueTask<ResultadoTokenBucket> TentarConsumirAsync(
        string chave, int capacidade, double reposicaoPorSegundo, CancellationToken cancellationToken);
}
