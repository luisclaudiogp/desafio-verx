using Lancamentos.Application;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Commands;
using Lancamentos.Infrastructure.Data;
using Lancamentos.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Verx.IntegrationTests.Infra;
using Verx.Persistence;
using Verx.SharedKernel;

namespace Verx.IntegrationTests;

public abstract class LancamentosIdempotenciaTestesBase(PostgresFixture postgres, ModoDePosicao modo)
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

    private Task<ResultadoRegistro> Estornar(Func<LancamentosDbContext> novo, EstornarLancamentoCommand comando) =>
        Task.Run(async () =>
        {
            await using var contexto = novo();
            var (replays, persistidor) = ColaboradoresDeLancamento.Criar(modo, contexto);
            return await new EstornarLancamentoHandler(new LancamentoRepository(contexto), replays, persistidor, TimeProvider.System)
                .HandleAsync(comando, CancellationToken.None);
        });

    [DockerFact]
    public async Task MesmaChaveEmParalelo_GeraUmUnicoLancamento()
    {
        var novo = await NovoBancoAsync();
        var comando = new RegistrarLancamentoCommand(Guid.NewGuid(), TipoMovimento.Credito, 50m, null, "chave-unica", "u");

        var resultados = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Registrar(novo, comando)));

        Assert.Equal(1, resultados.Count(r => !r.Replay));
        Assert.Single(resultados.Select(r => r.Lancamento.Id).Distinct());
        await using var contexto = novo();
        Assert.Equal(1, await contexto.Lancamentos.CountAsync());
    }

    [DockerFact]
    public async Task MesmaChaveEmContasDiferentes_SaoLancamentosIndependentes()
    {
        var novo = await NovoBancoAsync();

        var a = await Registrar(novo, new(Guid.NewGuid(), TipoMovimento.Credito, 1m, null, "k", "u"));
        var b = await Registrar(novo, new(Guid.NewGuid(), TipoMovimento.Credito, 1m, null, "k", "u"));

        Assert.False(a.Replay);
        Assert.False(b.Replay);
    }

    [DockerFact]
    public async Task EstornosConcorrentesComChavesDiferentes_SoUmPrevalece()
    {
        var novo = await NovoBancoAsync();
        var conta = Guid.NewGuid();
        var original = await Registrar(novo, new(conta, TipoMovimento.Credito, 25m, null, "orig", "u"));

        var tentativas = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            Estornar(novo, new EstornarLancamentoCommand(conta, original.Lancamento.Id, $"est-{i}", "u"))
                .ContinueWith(t => t, TaskScheduler.Default)));

        Assert.Equal(1, tentativas.Count(t => t.IsCompletedSuccessfully && !t.Result.Replay));
        Assert.All(tentativas.Where(t => t.IsFaulted), t => Assert.IsType<DomainException>(t.Exception!.InnerException));
        await using var contexto = novo();
        Assert.Equal(1, await contexto.Lancamentos.CountAsync(l => l.EstornoDeId == original.Lancamento.Id));
    }
}
