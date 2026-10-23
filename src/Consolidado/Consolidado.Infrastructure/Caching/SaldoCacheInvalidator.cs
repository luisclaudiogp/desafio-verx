using Consolidado.Application.Abstractions;

namespace Consolidado.Infrastructure.Caching;

public sealed class SaldoCacheInvalidator(IFailOpenCache cache) : ISaldoCacheInvalidator
{
    public Task InvalidarSaldoDiarioAsync(Guid contaId, DateOnly data, CancellationToken cancellationToken) =>
        cache.RemoveAsync(SaldoCacheKeys.Diario(contaId, data), cancellationToken);
}
