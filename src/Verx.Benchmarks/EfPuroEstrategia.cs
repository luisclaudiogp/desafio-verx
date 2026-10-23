using Consolidado.Domain.Entities;
using Consolidado.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Verx.Benchmarks;

public sealed class EfPuroEstrategia(Func<ConsolidadoDbContext> fabrica) : IEstrategiaDeProjecao
{
    public string Nome => "EF ingênuo (sem tratamento de conflito)";

    public long Chamadas => Interlocked.Read(ref _chamadas);

    public long Tentativas => Interlocked.Read(ref _chamadas);

    private long _chamadas;

    public async Task AplicarAsync(IReadOnlyList<Movimento> lote, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _chamadas);
        await using var contexto = fabrica();
        await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);

        var novos = await SomenteInedito(contexto, lote, cancellationToken);
        contexto.Movimentos.AddRange(novos);
        await contexto.SaveChangesAsync(cancellationToken);

        var faltantes = new List<SaldoDiario>();
        foreach (var delta in CalculadorDeDeltas.De(novos))
        {
            var afetadas = await contexto.Saldos
                .Where(s => s.ContaId == delta.ContaId && s.Data == delta.Data)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(x => x.TotalCreditos, x => x.TotalCreditos + delta.TotalCreditos)
                        .SetProperty(x => x.TotalDebitos, x => x.TotalDebitos + delta.TotalDebitos),
                    cancellationToken);

            if (afetadas == 0)
                faltantes.Add(delta);
        }

        if (faltantes.Count > 0)
        {
            contexto.Saldos.AddRange(faltantes);
            await contexto.SaveChangesAsync(cancellationToken);
        }

        await transacao.CommitAsync(cancellationToken);
    }

    private static async Task<List<Movimento>> SomenteInedito(
        ConsolidadoDbContext contexto, IReadOnlyList<Movimento> lote, CancellationToken cancellationToken)
    {
        var ids = lote.Select(m => m.LancamentoId).ToList();
        var existentes = await contexto.Movimentos.AsNoTracking()
            .Where(m => ids.Contains(m.LancamentoId))
            .Select(m => m.LancamentoId)
            .ToListAsync(cancellationToken);

        var conhecidos = existentes.ToHashSet();
        return lote.Where(m => !conhecidos.Contains(m.LancamentoId)).ToList();
    }
}
