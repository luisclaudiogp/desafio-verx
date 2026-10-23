using Consolidado.Application.Abstractions;
using Consolidado.Domain.Entities;
using Consolidado.Infrastructure.Data;
using Consolidado.Infrastructure.Projecao;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure.Snapshots;

public sealed class CorretorDeSnapshots(ConsolidadoDbContext context) : ICorretorDeSnapshots
{
    private const string Corrigir = """
        WITH afetados AS (
            SELECT s."ContaId", s."Data",
                   SUM(d.creditos) AS creditos, SUM(d.debitos) AS debitos
            FROM saldo_snapshots s
            JOIN unnest(@contas, @datas, @creditos, @debitos) AS d(conta, data, creditos, debitos)
              ON s."ContaId" = d.conta AND s."Data" >= d.data
            GROUP BY s."ContaId", s."Data"),
        travados AS (
            SELECT s."ContaId", s."Data"
            FROM saldo_snapshots s
            JOIN afetados a ON a."ContaId" = s."ContaId" AND a."Data" = s."Data"
            ORDER BY s."ContaId", s."Data"
            FOR UPDATE OF s)
        UPDATE saldo_snapshots s SET
            "TotalCreditos" = s."TotalCreditos" + a.creditos,
            "TotalDebitos" = s."TotalDebitos" + a.debitos
        FROM afetados a
        JOIN travados t ON t."ContaId" = a."ContaId" AND t."Data" = a."Data"
        WHERE s."ContaId" = a."ContaId" AND s."Data" = a."Data"
        """;

    public async Task CorrigirAsync(IReadOnlyCollection<SaldoDiario> deltas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deltas);
        if (deltas.Count == 0)
            return;

        await SnapshotLock.AdquirirCompartilhadoAsync(context, cancellationToken);
        await context.Database.ExecuteSqlRawAsync(Corrigir, DeltasSql.Parametros(deltas), cancellationToken);
    }
}
