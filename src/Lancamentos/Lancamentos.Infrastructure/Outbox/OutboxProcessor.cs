using System.Text.Json;
using Lancamentos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace Lancamentos.Infrastructure.Outbox;

public sealed class OutboxProcessor(LancamentosDbContext context, IBrokerPublisher broker, OutboxOptions options)
{
    public async Task<int> ProcessarLoteAsync(CancellationToken cancellationToken)
    {
        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);

        var limite = options.BatchSize;
        var itens = await context.OutboxEventos
            .FromSql($"""
                SELECT "Id", "Payload", "CriadoEm" FROM outbox_eventos
                ORDER BY "CriadoEm"
                LIMIT {limite}
                FOR UPDATE SKIP LOCKED
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (itens.Count == 0)
            return 0;

        var eventos = itens
            .Select(i => JsonSerializer.Deserialize<LancamentoCriadoEvent>(i.Payload)
                         ?? throw new InvalidOperationException($"Payload invalido no outbox: {i.Id}"))
            .ToList();

        await broker.PublicarLoteAsync(eventos, cancellationToken);

        var ids = itens.Select(i => i.Id).ToArray();
        await context.Database.ExecuteSqlAsync($"""DELETE FROM outbox_eventos WHERE "Id" = ANY({ids})""", cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        OutboxMetrics.Publicados.Add(itens.Count);
        OutboxMetrics.TamanhoDoLote.Record(itens.Count);
        return itens.Count;
    }
}
