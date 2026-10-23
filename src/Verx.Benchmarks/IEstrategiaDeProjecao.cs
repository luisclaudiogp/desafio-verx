using Consolidado.Domain.Entities;

namespace Verx.Benchmarks;

public interface IEstrategiaDeProjecao
{
    string Nome { get; }

    long Chamadas { get; }

    long Tentativas { get; }

    Task AplicarAsync(IReadOnlyList<Movimento> lote, CancellationToken cancellationToken);
}
