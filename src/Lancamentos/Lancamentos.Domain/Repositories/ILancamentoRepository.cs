using Lancamentos.Domain.Entities;

namespace Lancamentos.Domain.Repositories;

public interface ILancamentoRepository
{
    Task<Lancamento?> ObterPorIdAsync(Guid contaId, Guid lancamentoId, CancellationToken cancellationToken);

    Task<Lancamento?> ObterPorChaveAsync(Guid contaId, string chaveIdempotencia, CancellationToken cancellationToken);

    Task<bool> JaEstornadoAsync(Guid lancamentoId, CancellationToken cancellationToken);

    Task AdicionarAsync(Lancamento lancamento, CancellationToken cancellationToken);
}
