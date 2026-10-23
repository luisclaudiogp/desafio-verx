using MassTransit;
using Shared.Contracts;

namespace Lancamentos.Infrastructure.Outbox;

public sealed class MassTransitBrokerPublisher(IBus bus) : IBrokerPublisher
{
    public Task PublicarLoteAsync(IReadOnlyCollection<LancamentoCriadoEvent> eventos, CancellationToken cancellationToken) =>
        bus.PublishBatch(eventos, ExigirRoteamento, cancellationToken);

    private static void ExigirRoteamento(PublishContext<LancamentoCriadoEvent> contexto)
    {
        if (contexto.TryGetPayload<RabbitMqSendContext>(out var rabbit))
            rabbit.Mandatory = true;
    }
}
