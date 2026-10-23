using Consolidado.Domain.Entities;

namespace Consolidado.Application.Abstractions;

public interface ICorretorDeSnapshots
{
    Task CorrigirAsync(IReadOnlyCollection<SaldoDiario> deltas, CancellationToken cancellationToken);
}
