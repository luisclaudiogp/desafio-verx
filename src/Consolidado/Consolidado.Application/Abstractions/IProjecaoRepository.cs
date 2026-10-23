namespace Consolidado.Application.Abstractions;

public interface IProjecaoRepository
{
    Task<IProjecaoTransacao> IniciarAsync(CancellationToken cancellationToken);

    Task RegistrarRejeicoesAsync(IReadOnlyCollection<RejeicaoMovimento> rejeicoes, CancellationToken cancellationToken);
}
