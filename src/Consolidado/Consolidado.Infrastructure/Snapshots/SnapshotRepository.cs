using Consolidado.Application.Abstractions;
using Consolidado.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Consolidado.Infrastructure.Snapshots;

public sealed class SnapshotRepository(ConsolidadoDbContext context) : ISnapshotRepository
{
    private const string GerarLote = """
        WITH contas AS (
            SELECT d."ContaId"
            FROM saldos_diarios d
            WHERE d."Data" <= @ate
              AND NOT EXISTS (
                  SELECT 1 FROM saldo_snapshots s
                  WHERE s."ContaId" = d."ContaId" AND s."Data" >= d."Data")
            GROUP BY d."ContaId"
            ORDER BY d."ContaId"
            LIMIT @lote),
        ultimos AS (
            SELECT DISTINCT ON (s."ContaId") s."ContaId", s."Data", s."TotalCreditos", s."TotalDebitos"
            FROM saldo_snapshots s
            JOIN contas c ON c."ContaId" = s."ContaId"
            ORDER BY s."ContaId", s."Data" DESC),
        novos AS (
            SELECT d."ContaId", d."Data",
                   COALESCE(u."TotalCreditos", 0) + SUM(d."TotalCreditos") OVER w AS creditos,
                   COALESCE(u."TotalDebitos", 0) + SUM(d."TotalDebitos") OVER w AS debitos
            FROM saldos_diarios d
            JOIN contas c ON c."ContaId" = d."ContaId"
            LEFT JOIN ultimos u ON u."ContaId" = d."ContaId"
            WHERE d."Data" <= @ate AND (u."Data" IS NULL OR d."Data" > u."Data")
            WINDOW w AS (PARTITION BY d."ContaId" ORDER BY d."Data"))
        INSERT INTO saldo_snapshots ("ContaId", "Data", "TotalCreditos", "TotalDebitos", "GeradoEm")
        SELECT "ContaId", "Data", creditos, debitos, @agora FROM novos
        ON CONFLICT ("ContaId", "Data") DO NOTHING
        """;

    public async Task<int> GerarLoteAsync(DateOnly ate, int loteContas, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);

        await SnapshotLock.AdquirirExclusivoAsync(context, cancellationToken);
        var gerados = await context.Database.ExecuteSqlRawAsync(
            GerarLote,
            [
                new NpgsqlParameter<DateOnly>("ate", ate),
                new NpgsqlParameter<int>("lote", loteContas),
                new NpgsqlParameter<DateTimeOffset>("agora", agora.ToUniversalTime())
            ],
            cancellationToken);

        await transacao.CommitAsync(cancellationToken);
        return gerados;
    }
}
