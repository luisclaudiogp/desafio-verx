using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure.Snapshots;

public static class SnapshotLock
{
    private const long Chave = 7_301_001L;

    public static Task AdquirirExclusivoAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({Chave})", cancellationToken);

    public static Task AdquirirCompartilhadoAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared({Chave})", cancellationToken);
}
