using System.Text.Json;
using Consolidado.Application;
using Consolidado.Application.Abstractions;

namespace Consolidado.Infrastructure.Caching;

public sealed class CachedSaldoDiarioReadRepository(
    ISaldoDiarioReadRepository inner,
    IFailOpenCache cache) : ISaldoDiarioReadRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public async Task<SaldoDiarioDto> ObterSaldoDiarioAsync(Guid contaId, DateOnly data, CancellationToken cancellationToken)
    {
        var chave = SaldoCacheKeys.Diario(contaId, data);

        var json = await cache.GetStringAsync(chave, cancellationToken);
        if (!string.IsNullOrEmpty(json) && JsonSerializer.Deserialize<SaldoDiarioDto>(json) is { } emCache)
            return emCache;

        var saldo = await inner.ObterSaldoDiarioAsync(contaId, data, cancellationToken);
        await cache.SetStringAsync(chave, JsonSerializer.Serialize(saldo), Ttl, cancellationToken);
        return saldo;
    }
}
