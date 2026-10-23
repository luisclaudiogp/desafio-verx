using Lancamentos.Domain.Repositories;
using Verx.SharedKernel;

namespace Lancamentos.Application.Queries;

public sealed class ObterLancamentoHandler(ILancamentoRepository repository)
    : IQueryHandler<ObterLancamentoQuery, LancamentoDto>
{
    public async Task<LancamentoDto> HandleAsync(ObterLancamentoQuery query, CancellationToken cancellationToken)
    {
        var lancamento = await repository.ObterPorIdAsync(query.ContaId, query.LancamentoId, cancellationToken)
            ?? throw new NaoEncontradoException("Lancamento nao encontrado.");
        return LancamentoDto.De(lancamento);
    }
}
