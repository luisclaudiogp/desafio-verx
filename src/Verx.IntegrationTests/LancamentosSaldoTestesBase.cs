using Lancamentos.Application;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Commands;
using Lancamentos.Domain.Exceptions;
using Lancamentos.Infrastructure.Data;
using Lancamentos.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Verx.IntegrationTests.Infra;
using Verx.Persistence;
using Verx.SharedKernel;

namespace Verx.IntegrationTests;

public abstract class LancamentosSaldoTestesBase(PostgresFixture postgres, ModoDePosicao modo)
{
    private Task<Func<LancamentosDbContext>> NovoBancoAsync() =>
        postgres.NovoBancoAsync<LancamentosDbContext>(o => new LancamentosDbContext(o));

    private Task<ResultadoRegistro> Registrar(Func<LancamentosDbContext> novo, RegistrarLancamentoCommand comando) =>
        Task.Run(async () =>
        {
            await using var contexto = novo();
            var (replays, persistidor) = ColaboradoresDeLancamento.Criar(modo, contexto);
            return await new RegistrarLancamentoHandler(replays, persistidor, TimeProvider.System)
                .HandleAsync(comando, CancellationToken.None);
        });

    private async Task<Exception?> Tentar(Func<LancamentosDbContext> novo, RegistrarLancamentoCommand comando)
    {
        try
        {
            await Registrar(novo, comando);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task<decimal> SaldoDoLedger(LancamentosDbContext contexto, Guid conta)
    {
        var lancamentos = await contexto.Lancamentos.Where(l => l.ContaId == conta).ToListAsync();
        return lancamentos.Sum(l => l.Tipo == TipoMovimento.Credito ? l.Valor.Valor : -l.Valor.Valor);
    }

    [DockerFact]
    public async Task DebitosConcorrentes_NuncaDeixamSaldoNegativoENemGastamAMais()
    {
        var novo = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        await Registrar(novo, new(conta, TipoMovimento.Credito, 100m, null, "carga-inicial", "u"));

        var resultados = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(i => Tentar(novo, new(conta, TipoMovimento.Debito, 30m, null, $"deb-{i}", "u"))));

        Assert.Equal(3, resultados.Count(r => r is null));
        Assert.Equal(7, resultados.Count(r => r is SaldoInsuficienteException));
        await using var contexto = novo();
        Assert.Equal(10m, (await contexto.Posicoes.SingleAsync(p => p.ContaId == conta)).Saldo);
        Assert.Equal(10m, await SaldoDoLedger(contexto, conta));
        Assert.Equal(4, await contexto.Lancamentos.CountAsync(l => l.ContaId == conta));
    }

    [DockerFact]
    public async Task CreditosEDebitosMisturados_PosicaoSempreIgualAoLedger()
    {
        var novo = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        await Registrar(novo, new(conta, TipoMovimento.Credito, 500m, null, "inicial", "u"));

        var resultados = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Tentar(
            novo,
            new(conta, i % 2 == 0 ? TipoMovimento.Credito : TipoMovimento.Debito, 20m, null, $"m-{i}", "u"))));

        Assert.All(resultados, r => Assert.True(r is null or SaldoInsuficienteException || (modo == ModoDePosicao.Ef && r is ConcorrenciaException)));
        await using var contexto = novo();
        var posicao = (await contexto.Posicoes.SingleAsync(p => p.ContaId == conta)).Saldo;
        Assert.True(posicao >= 0);
        Assert.Equal(await SaldoDoLedger(contexto, conta), posicao);
    }

    [DockerFact]
    public async Task DebitoRecusado_NaoGravaLancamentoNemEvento()
    {
        var novo = await NovoBancoAsync();
        var conta = Guid.NewGuid();

        var erro = await Tentar(novo, new(conta, TipoMovimento.Debito, 10m, null, "sem-saldo", "u"));

        Assert.IsType<SaldoInsuficienteException>(erro);
        await using var contexto = novo();
        Assert.Equal(0, await contexto.Lancamentos.CountAsync(l => l.ContaId == conta));
        Assert.False(await contexto.Posicoes.AnyAsync(p => p.ContaId == conta));
    }

    [DockerFact]
    public async Task MesmaChaveComCorpoDiferente_ERecusadaEMesmoCorpoViraReplay()
    {
        var novo = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        var original = await Registrar(novo, new(conta, TipoMovimento.Credito, 50m, null, "k", "u"));

        var replay = await Registrar(novo, new(conta, TipoMovimento.Credito, 50m, null, "k", "u"));
        var erro = await Tentar(novo, new(conta, TipoMovimento.Credito, 51m, null, "k", "u"));

        Assert.True(replay.Replay);
        Assert.Equal(original.Lancamento.Id, replay.Lancamento.Id);
        Assert.IsType<ChaveIdempotenciaReutilizadaException>(erro);
        await using var contexto = novo();
        Assert.Equal(1, await contexto.Lancamentos.CountAsync(l => l.ContaId == conta));
        Assert.Equal(50m, (await contexto.Posicoes.SingleAsync(p => p.ContaId == conta)).Saldo);
    }
}
