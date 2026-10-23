using Consolidado.Application;
using Consolidado.Application.Abstractions;
using Consolidado.Domain.Tempo;
using Consolidado.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Verx.SharedKernel;

namespace Consolidado.Infrastructure.Repositories;

public sealed class SaldoReadRepository(ConsolidadoDbContext context) : ISaldoDiarioReadRepository, ISaldoEmReadRepository
{
    public async Task<SaldoDiarioDto> ObterSaldoDiarioAsync(Guid contaId, DateOnly data, CancellationToken cancellationToken)
    {
        var saldo = await context.Saldos.AsNoTracking()
            .Where(s => s.ContaId == contaId && s.Data == data)
            .Select(s => new { s.TotalCreditos, s.TotalDebitos })
            .FirstOrDefaultAsync(cancellationToken);

        var creditos = saldo?.TotalCreditos ?? 0m;
        var debitos = saldo?.TotalDebitos ?? 0m;
        return new SaldoDiarioDto(contaId, data, creditos, debitos, creditos - debitos);
    }

    public async Task<SaldoEmDto> ObterSaldoEmAsync(Guid contaId, DateTimeOffset em, CancellationToken cancellationToken)
    {
        var instante = em.ToUniversalTime();
        var dia = instante.DiaUtc();

        var snapshot = await context.Snapshots.AsNoTracking()
            .Where(s => s.ContaId == contaId && s.Data < dia)
            .OrderByDescending(s => s.Data)
            .Select(s => new { s.Data, s.TotalCreditos, s.TotalDebitos })
            .FirstOrDefaultAsync(cancellationToken);

        var dataDoSnapshot = snapshot?.Data;
        var intermediarios = await context.Saldos.AsNoTracking()
            .Where(s => s.ContaId == contaId && s.Data < dia && (dataDoSnapshot == null || s.Data > dataDoSnapshot))
            .Select(s => new { s.TotalCreditos, s.TotalDebitos })
            .ToListAsync(cancellationToken);

        var doDia = await context.Movimentos.AsNoTracking()
            .Where(m => m.ContaId == contaId && m.OcorridoEm >= dia.InicioDoDiaUtc() && m.OcorridoEm <= instante)
            .GroupBy(m => m.Tipo)
            .Select(g => new { Tipo = g.Key, Total = g.Sum(m => m.Valor) })
            .ToListAsync(cancellationToken);

        var creditos = (snapshot?.TotalCreditos ?? 0m)
                       + intermediarios.Sum(s => s.TotalCreditos)
                       + (doDia.FirstOrDefault(t => t.Tipo == TipoMovimento.Credito)?.Total ?? 0m);
        var debitos = (snapshot?.TotalDebitos ?? 0m)
                      + intermediarios.Sum(s => s.TotalDebitos)
                      + (doDia.FirstOrDefault(t => t.Tipo == TipoMovimento.Debito)?.Total ?? 0m);

        return new SaldoEmDto(contaId, em, creditos, debitos, creditos - debitos);
    }
}
