using Consolidado.Domain.Entities;

namespace Consolidado.Application.Abstractions;

public interface IProjecaoTransacao : IAsyncDisposable
{
    Task<IReadOnlyList<Movimento>> InserirMovimentosAsync(IReadOnlyList<Movimento> movimentos, CancellationToken cancellationToken);

    Task AcumularSaldosAsync(IReadOnlyCollection<SaldoDiario> deltas, CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);
}
