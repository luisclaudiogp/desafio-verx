using System.Collections.Concurrent;
using System.Diagnostics;
using Consolidado.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Verx.SharedKernel;

namespace Verx.Benchmarks;

public sealed class ExperimentoDeConcorrencia(int rodadas, int trabalhadores, int contasPorRodada, int eventosPorLote)
{
    public async Task<ResultadoDeConcorrencia> ExecutarAsync(IEstrategiaDeProjecao estrategia, Func<ConsolidadoDbContext> fabrica)
    {
        var falhas = new ConcurrentBag<string>();
        var relogio = Stopwatch.StartNew();

        for (var rodada = 0; rodada < rodadas; rodada++)
            await ExecutarRodadaAsync(estrategia, rodada, falhas);

        relogio.Stop();
        var motivo = falhas.GroupBy(f => f).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        return new ResultadoDeConcorrencia(
            estrategia.Nome, rodadas * trabalhadores, falhas.Count, estrategia.Tentativas - estrategia.Chamadas, motivo, await EstaConsistenteAsync(fabrica), relogio.Elapsed.TotalMilliseconds);
    }

    private async Task ExecutarRodadaAsync(IEstrategiaDeProjecao estrategia, int rodada, ConcurrentBag<string> falhas)
    {
        var contas = GeradorDeMovimentos.NovasContas(contasPorRodada);
        var lotes = Enumerable.Range(0, trabalhadores)
            .Select(t => new GeradorDeMovimentos((rodada * 1000) + t).Lote(contas, eventosPorLote))
            .ToList();

        using var largada = new Barrier(trabalhadores);
        await Task.WhenAll(lotes.Select(lote => Task.Run(async () =>
        {
            largada.SignalAndWait();
            try
            {
                await estrategia.AplicarAsync(lote, CancellationToken.None);
            }
            catch (Exception ex)
            {
                falhas.Add((ex.InnerException ?? ex).Message.Split('\n')[0]);
            }
        })));
    }

    private static async Task<bool> EstaConsistenteAsync(Func<ConsolidadoDbContext> fabrica)
    {
        await using var contexto = fabrica();
        var movimentos = await contexto.Movimentos.AsNoTracking().ToListAsync();
        var saldos = await contexto.Saldos.AsNoTracking().ToListAsync();

        var creditosDosMovimentos = movimentos.Where(m => m.Tipo == TipoMovimento.Credito).Sum(m => m.Valor);
        var debitosDosMovimentos = movimentos.Where(m => m.Tipo == TipoMovimento.Debito).Sum(m => m.Valor);
        return creditosDosMovimentos == saldos.Sum(s => s.TotalCreditos) && debitosDosMovimentos == saldos.Sum(s => s.TotalDebitos);
    }
}
