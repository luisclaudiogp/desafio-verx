using Lancamentos.Domain.Entities;

namespace Lancamentos.Domain.Repositories;

public interface IPosicaoContaRepository
{
    Task<PosicaoConta> ObterParaAtualizacaoAsync(Guid contaId, CancellationToken cancellationToken);
}
