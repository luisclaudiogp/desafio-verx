using System.Text.Json;
using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Entities;
using Lancamentos.Infrastructure.Data;

namespace Lancamentos.Infrastructure.Outbox;

public sealed class OutboxLancamentoEventPublisher(LancamentosDbContext context, TimeProvider timeProvider)
    : ILancamentoEventPublisher
{
    public async Task PublicarCriacaoAsync(Lancamento lancamento, CancellationToken cancellationToken)
    {
        var evento = LancamentoEventMapper.ParaEvento(lancamento);
        var payload = JsonSerializer.Serialize(evento);

        await context.OutboxEventos.AddAsync(
            new OutboxEvento(lancamento.Id, payload, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}
