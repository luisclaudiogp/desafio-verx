using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Infrastructure.Repositories;

public sealed class LancamentoRepository(LancamentosDbContext context) : ILancamentoRepository
{
    public Task<Lancamento?> ObterPorIdAsync(Guid contaId, Guid lancamentoId, CancellationToken cancellationToken) =>
        context.Lancamentos.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lancamentoId && l.ContaId == contaId, cancellationToken);

    public Task<Lancamento?> ObterPorChaveAsync(Guid contaId, string chaveIdempotencia, CancellationToken cancellationToken) =>
        context.Lancamentos.AsNoTracking()
            .FirstOrDefaultAsync(l => l.ContaId == contaId && l.ChaveIdempotencia == chaveIdempotencia, cancellationToken);

    public Task<bool> JaEstornadoAsync(Guid lancamentoId, CancellationToken cancellationToken) =>
        context.Lancamentos.AnyAsync(l => l.EstornoDeId == lancamentoId, cancellationToken);

    public async Task AdicionarAsync(Lancamento lancamento, CancellationToken cancellationToken) =>
        await context.Lancamentos.AddAsync(lancamento, cancellationToken);
}
