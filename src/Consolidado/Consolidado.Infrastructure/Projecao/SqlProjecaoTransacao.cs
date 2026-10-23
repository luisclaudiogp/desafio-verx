using Consolidado.Application.Abstractions;
using Consolidado.Domain.Entities;
using Consolidado.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Verx.SharedKernel;

namespace Consolidado.Infrastructure.Projecao;

public sealed class SqlProjecaoTransacao(ConsolidadoDbContext context, IDbContextTransaction transacao) : IProjecaoTransacao
{
    private const string InserirMovimentos = """
        INSERT INTO movimentos ("LancamentoId", "ContaId", "Tipo", "Valor", "OcorridoEm", "ProcessadoEm")
        SELECT * FROM unnest(@ids, @contas, @tipos, @valores, @ocorridos, @processados)
        ON CONFLICT ("LancamentoId") DO NOTHING
        RETURNING "LancamentoId" AS "Value"
        """;

    private const string AcumularSaldos = """
        INSERT INTO saldos_diarios ("ContaId", "Data", "TotalCreditos", "TotalDebitos")
        SELECT * FROM unnest(@contas, @datas, @creditos, @debitos)
        ON CONFLICT ("ContaId", "Data") DO UPDATE SET
            "TotalCreditos" = saldos_diarios."TotalCreditos" + EXCLUDED."TotalCreditos",
            "TotalDebitos" = saldos_diarios."TotalDebitos" + EXCLUDED."TotalDebitos"
        """;

    public async Task<IReadOnlyList<Movimento>> InserirMovimentosAsync(
        IReadOnlyList<Movimento> movimentos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movimentos);

        var inseridos = await context.Database
            .SqlQueryRaw<Guid>(
                InserirMovimentos,
                new NpgsqlParameter<Guid[]>("ids", movimentos.Select(m => m.LancamentoId).ToArray()),
                new NpgsqlParameter<Guid[]>("contas", movimentos.Select(m => m.ContaId).ToArray()),
                new NpgsqlParameter<short[]>("tipos", movimentos.Select(m => (short)m.Tipo).ToArray()),
                new NpgsqlParameter<decimal[]>("valores", movimentos.Select(m => m.Valor).ToArray()),
                new NpgsqlParameter<DateTimeOffset[]>("ocorridos", movimentos.Select(m => m.OcorridoEm).ToArray()),
                new NpgsqlParameter<DateTimeOffset[]>("processados", movimentos.Select(m => m.ProcessadoEm).ToArray()))
            .ToListAsync(cancellationToken);

        var aplicados = inseridos.ToHashSet();
        return movimentos.Where(m => aplicados.Contains(m.LancamentoId)).ToList();
    }

    public async Task AcumularSaldosAsync(IReadOnlyCollection<SaldoDiario> deltas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deltas);
        if (deltas.Count == 0)
            return;

        await context.Database.ExecuteSqlRawAsync(AcumularSaldos, DeltasSql.Parametros(deltas), cancellationToken);
    }

    public Task CommitAsync(CancellationToken cancellationToken) => transacao.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transacao.DisposeAsync();
}
