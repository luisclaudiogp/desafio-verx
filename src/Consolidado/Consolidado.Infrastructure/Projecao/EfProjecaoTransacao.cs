using Consolidado.Application.Abstractions;
using Consolidado.Domain.Entities;
using Consolidado.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Verx.SharedKernel;

namespace Consolidado.Infrastructure.Projecao;

public sealed class EfProjecaoTransacao(ConsolidadoDbContext context, IDbContextTransaction transacao) : IProjecaoTransacao
{
    public Task<IReadOnlyList<Movimento>> InserirMovimentosAsync(
        IReadOnlyList<Movimento> movimentos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movimentos);

        return ExecutarAsync(async () =>
        {
            var ids = movimentos.Select(m => m.LancamentoId).ToList();
            var existentes = (await context.Movimentos.AsNoTracking()
                    .Where(m => ids.Contains(m.LancamentoId))
                    .Select(m => m.LancamentoId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var novos = movimentos.Where(m => !existentes.Contains(m.LancamentoId)).ToList();
            context.Movimentos.AddRange(novos);
            await context.SaveChangesAsync(cancellationToken);
            return (IReadOnlyList<Movimento>)novos;
        });
    }

    public Task AcumularSaldosAsync(IReadOnlyCollection<SaldoDiario> deltas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deltas);

        return ExecutarAsync(async () =>
        {
            foreach (var delta in deltas.OrderBy(d => d.ContaId).ThenBy(d => d.Data))
            {
                var afetadas = await context.Saldos
                    .Where(s => s.ContaId == delta.ContaId && s.Data == delta.Data)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(x => x.TotalCreditos, x => x.TotalCreditos + delta.TotalCreditos)
                            .SetProperty(x => x.TotalDebitos, x => x.TotalDebitos + delta.TotalDebitos),
                        cancellationToken);

                if (afetadas > 0)
                    continue;

                context.Saldos.Add(delta);
                await context.SaveChangesAsync(cancellationToken);
            }

            return true;
        });
    }

    public Task CommitAsync(CancellationToken cancellationToken) => transacao.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => transacao.DisposeAsync();

    private async Task<T> ExecutarAsync<T>(Func<Task<T>> operacao)
    {
        try
        {
            return await operacao();
        }
        catch (Exception ex) when (ConflitoTransitorio.Eh(ex))
        {
            context.ChangeTracker.Clear();
            throw new ConcorrenciaException("Conflito transitorio na projecao; o lote deve ser repetido.", ex);
        }
    }
}
