using Consolidado.Application.Abstractions;
using Verx.SharedKernel;

namespace Consolidado.Application.Queries;

public sealed class ObterSaldoEmHandler(ISaldoEmReadRepository repository)
    : IQueryHandler<ObterSaldoEmQuery, SaldoEmDto>
{
    public Task<SaldoEmDto> HandleAsync(ObterSaldoEmQuery query, CancellationToken cancellationToken) =>
        repository.ObterSaldoEmAsync(query.ContaId, query.Em, cancellationToken);
}
