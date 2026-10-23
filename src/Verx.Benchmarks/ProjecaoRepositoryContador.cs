using Consolidado.Application;
using Consolidado.Application.Abstractions;

namespace Verx.Benchmarks;

public sealed class ProjecaoRepositoryContador(IProjecaoRepository interno) : IProjecaoRepository
{
    private long _inicios;

    public long Inicios => Interlocked.Read(ref _inicios);

    public Task<IProjecaoTransacao> IniciarAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _inicios);
        return interno.IniciarAsync(cancellationToken);
    }

    public Task RegistrarRejeicoesAsync(IReadOnlyCollection<RejeicaoMovimento> rejeicoes, CancellationToken cancellationToken) =>
        interno.RegistrarRejeicoesAsync(rejeicoes, cancellationToken);
}
