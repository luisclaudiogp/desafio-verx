using Consolidado.Application.Abstractions;
using Verx.SharedKernel;

namespace Consolidado.Application.Queries;

public sealed class ObterSaldoDiarioHandler(ISaldoDiarioReadRepository repository)
    : IQueryHandler<ObterSaldoDiarioQuery, SaldoDiarioDto>
{
    public Task<SaldoDiarioDto> HandleAsync(ObterSaldoDiarioQuery query, CancellationToken cancellationToken) =>
        repository.ObterSaldoDiarioAsync(query.ContaId, query.Data, cancellationToken);
}
