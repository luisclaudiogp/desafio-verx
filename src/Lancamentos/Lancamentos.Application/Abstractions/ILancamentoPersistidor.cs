using Lancamentos.Domain.Entities;

namespace Lancamentos.Application.Abstractions;

public interface ILancamentoPersistidor
{
    Task<ResultadoRegistro?> PersistirAsync(Lancamento lancamento, CancellationToken cancellationToken);
}
