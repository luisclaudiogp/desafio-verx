using Lancamentos.Domain.Entities;

namespace Lancamentos.Application.Abstractions;

public interface ILancamentoEventPublisher
{
    Task PublicarCriacaoAsync(Lancamento lancamento, CancellationToken cancellationToken);
}
