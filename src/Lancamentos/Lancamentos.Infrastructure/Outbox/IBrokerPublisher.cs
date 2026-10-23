using Shared.Contracts;

namespace Lancamentos.Infrastructure.Outbox;

public interface IBrokerPublisher
{
    Task PublicarLoteAsync(IReadOnlyCollection<LancamentoCriadoEvent> eventos, CancellationToken cancellationToken);
}
