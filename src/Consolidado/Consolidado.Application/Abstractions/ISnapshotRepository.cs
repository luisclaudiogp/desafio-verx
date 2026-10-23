namespace Consolidado.Application.Abstractions;

public interface ISnapshotRepository
{
    Task<int> GerarLoteAsync(DateOnly ate, int loteContas, DateTimeOffset agora, CancellationToken cancellationToken);
}
