using System.Collections.Concurrent;
using System.Diagnostics;
using Lancamentos.Application;
using Lancamentos.Application.Commands;
using Lancamentos.Domain.Exceptions;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure.Data;
using Lancamentos.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Verx.Persistence;
using Verx.SharedKernel;

namespace Verx.Benchmarks;

public sealed class ExperimentoDePosicao(int operacoes, int contas, int paralelismo)
{
    public async Task<ResultadoDePosicao> ExecutarAsync(ModoDePosicao modo, string conexaoBase)
    {
        await using var banco = await BancoDeTeste.CriarAsync<LancamentosDbContext>(conexaoBase, o => new LancamentosDbContext(o));
        var ids = GeradorDeMovimentos.NovasContas(contas);

        foreach (var conta in ids)
            await RegistrarAsync(banco.Fabrica, modo, new RegistrarLancamentoCommand(conta, TipoMovimento.Credito, 1_000_000m, null, "carga-" + conta, "bench"));

        var latencias = new ConcurrentBag<double>();
        var aceitas = 0;
        var insuficientes = 0;
        var conflitos = 0;
        var aleatorio = new Random(5);
        var comandos = Enumerable.Range(0, operacoes)
            .Select(i => new RegistrarLancamentoCommand(
                ids[aleatorio.Next(ids.Count)],
                i % 5 < 3 ? TipoMovimento.Credito : TipoMovimento.Debito,
                aleatorio.Next(1, 100),
                null,
                "op-" + i,
                "bench"))
            .ToList();

        using var limite = new SemaphoreSlim(paralelismo);
        var relogio = Stopwatch.StartNew();
        await Task.WhenAll(comandos.Select(comando => Task.Run(async () =>
        {
            await limite.WaitAsync();
            var inicio = Stopwatch.StartNew();
            try
            {
                await RegistrarAsync(banco.Fabrica, modo, comando);
                Interlocked.Increment(ref aceitas);
            }
            catch (SaldoInsuficienteException)
            {
                Interlocked.Increment(ref insuficientes);
            }
            catch (ConcorrenciaException)
            {
                Interlocked.Increment(ref conflitos);
            }
            finally
            {
                latencias.Add(inicio.Elapsed.TotalMilliseconds);
                limite.Release();
            }
        })));
        relogio.Stop();

        var ordenadas = latencias.Order().ToList();
        return new ResultadoDePosicao(
            modo == ModoDePosicao.Sql ? "SQL (FOR UPDATE)" : "EF (concorrência otimista, padrão)",
            contas,
            operacoes,
            operacoes / relogio.Elapsed.TotalSeconds,
            ordenadas[ordenadas.Count / 2],
            ordenadas[(int)Math.Ceiling(ordenadas.Count * 0.95) - 1],
            aceitas,
            insuficientes,
            conflitos,
            await PosicaoIgualAoLedgerAsync(banco.Fabrica));
    }

    private static async Task RegistrarAsync(Func<LancamentosDbContext> fabrica, ModoDePosicao modo, RegistrarLancamentoCommand comando)
    {
        await using var contexto = fabrica();
        var repositorio = new LancamentoRepository(contexto);
        var replays = new VerificadorDeReplay(repositorio);
        IPosicaoContaRepository posicoes = modo == ModoDePosicao.Sql
            ? new SqlPosicaoContaRepository(contexto)
            : new EfPosicaoContaRepository(contexto);
        var persistidor = new LancamentoPersistidor(
            repositorio, posicoes, new PublicadorNulo(), new EfUnitOfWork<LancamentosDbContext>(contexto), replays, TimeProvider.System);

        await new RegistrarLancamentoHandler(replays, persistidor, TimeProvider.System).HandleAsync(comando, CancellationToken.None);
    }

    private static async Task<bool> PosicaoIgualAoLedgerAsync(Func<LancamentosDbContext> fabrica)
    {
        await using var contexto = fabrica();
        var lancamentos = await contexto.Lancamentos.AsNoTracking().ToListAsync();
        var ledger = lancamentos
            .GroupBy(l => l.ContaId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Tipo == TipoMovimento.Credito ? l.Valor.Valor : -l.Valor.Valor));
        var posicoes = await contexto.Posicoes.AsNoTracking().ToDictionaryAsync(p => p.ContaId, p => p.Saldo);

        return ledger.Count == posicoes.Count && ledger.All(par => posicoes[par.Key] == par.Value) && posicoes.Values.All(s => s >= 0);
    }
}
