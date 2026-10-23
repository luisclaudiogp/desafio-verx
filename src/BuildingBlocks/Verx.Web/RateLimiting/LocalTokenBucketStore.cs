using System.Collections.Concurrent;

namespace Verx.Web.RateLimiting;

public sealed class LocalTokenBucketStore(TimeProvider timeProvider) : ITokenBucketStore
{
    private static readonly TimeSpan IntervaloDeLimpeza = TimeSpan.FromMinutes(1);

    private sealed class Balde(double tokens, long ultimoInstante, double segundosParaEncher)
    {
        public object Trava { get; } = new();
        public double Tokens { get; set; } = tokens;
        public long UltimoInstante { get; set; } = ultimoInstante;
        public double SegundosParaEncher { get; } = segundosParaEncher;
    }

    private readonly ConcurrentDictionary<string, Balde> _baldes = new();
    private long _proximaLimpeza = timeProvider.GetTimestamp();

    public int QuantidadeDeBaldes => _baldes.Count;

    public ValueTask<ResultadoTokenBucket> TentarConsumirAsync(
        string chave, int capacidade, double reposicaoPorSegundo, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(chave);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacidade, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reposicaoPorSegundo, 0d);

        var agora = timeProvider.GetTimestamp();
        var balde = _baldes.GetOrAdd(chave, _ => new Balde(capacidade, agora, capacidade / reposicaoPorSegundo));

        ResultadoTokenBucket resultado;
        lock (balde.Trava)
        {
            var decorrido = timeProvider.GetElapsedTime(balde.UltimoInstante, agora).TotalSeconds;
            balde.Tokens = Math.Min(capacidade, balde.Tokens + (decorrido * reposicaoPorSegundo));
            balde.UltimoInstante = agora;

            if (balde.Tokens >= 1d)
            {
                balde.Tokens -= 1d;
                resultado = new ResultadoTokenBucket(true, null);
            }
            else
            {
                resultado = new ResultadoTokenBucket(false, TimeSpan.FromSeconds((1d - balde.Tokens) / reposicaoPorSegundo));
            }
        }

        Limpar(agora);
        return ValueTask.FromResult(resultado);
    }

    private void Limpar(long agora)
    {
        var proxima = Interlocked.Read(ref _proximaLimpeza);
        if (timeProvider.GetElapsedTime(proxima, agora) < IntervaloDeLimpeza)
            return;

        if (Interlocked.CompareExchange(ref _proximaLimpeza, agora, proxima) != proxima)
            return;

        foreach (var (chave, balde) in _baldes)
        {
            bool ocioso;
            lock (balde.Trava)
                ocioso = timeProvider.GetElapsedTime(balde.UltimoInstante, agora).TotalSeconds > balde.SegundosParaEncher;

            if (ocioso)
                _baldes.TryRemove(new KeyValuePair<string, Balde>(chave, balde));
        }
    }
}
