using System.Collections.Concurrent;
using Lancamentos.Domain.Entities;
using Lancamentos.Infrastructure.Data;
using Lancamentos.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Verx.IntegrationTests.Infra;
using Verx.SharedKernel;

namespace Verx.IntegrationTests;

[Collection(PostgresTestes.Nome)]
public class OutboxRelayTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private Task<Func<LancamentosDbContext>> NovoBancoAsync() =>
        postgres.NovoBancoAsync<LancamentosDbContext>(o => new LancamentosDbContext(o));

    private static async Task<List<Guid>> EnfileirarAsync(Func<LancamentosDbContext> novo, int quantidade)
    {
        await using var contexto = novo();
        var publisher = new OutboxLancamentoEventPublisher(contexto, TimeProvider.System);
        var ids = new List<Guid>();

        for (var i = 0; i < quantidade; i++)
        {
            var lancamento = Lancamento.Registrar(
                new NovoLancamento(Guid.NewGuid(), TipoMovimento.Credito, 10m, null, $"k-{Guid.NewGuid()}", "u"), Agora);
            await contexto.Lancamentos.AddAsync(lancamento);
            await publisher.PublicarCriacaoAsync(lancamento, CancellationToken.None);
            ids.Add(lancamento.Id);
        }

        await contexto.SaveChangesAsync();
        return ids;
    }

    private static async Task DrenarAsync(Func<LancamentosDbContext> novo, FakeBroker broker, int workers, int lote)
    {
        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
        {
            while (true)
            {
                await using var contexto = novo();
                var processados = await new OutboxProcessor(contexto, broker, new OutboxOptions { BatchSize = lote })
                    .ProcessarLoteAsync(CancellationToken.None);
                if (processados == 0) return;
            }
        })));
    }

    [DockerFact]
    public async Task WorkersConcorrentes_PublicamCadaEventoExatamenteUmaVezEEsvaziamOOutbox()
    {
        var novo = await NovoBancoAsync();
        var ids = await EnfileirarAsync(novo, 2000);
        var broker = new FakeBroker();

        await DrenarAsync(novo, broker, workers: 6, lote: 150);

        Assert.Equal(2000, broker.Publicados.Count);
        Assert.Equal(ids.Order(), broker.Publicados.Select(e => e.LancamentoId).Order());
        await using var contexto = novo();
        Assert.Equal(0, await contexto.OutboxEventos.CountAsync());
    }

    [DockerFact]
    public async Task BrokerIndisponivel_PreservaOsEventosEEntregaQuandoRecuperar()
    {
        var novo = await NovoBancoAsync();
        await EnfileirarAsync(novo, 50);
        var broker = new FakeBroker { Falhar = true };

        await using (var contexto = novo())
        {
            var processor = new OutboxProcessor(contexto, broker, new OutboxOptions { BatchSize = 100 });
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessarLoteAsync(CancellationToken.None));
        }

        await using (var contexto = novo())
            Assert.Equal(50, await contexto.OutboxEventos.CountAsync());

        broker.Falhar = false;
        await DrenarAsync(novo, broker, workers: 2, lote: 100);

        Assert.Equal(50, broker.Publicados.Count);
        await using var final = novo();
        Assert.Equal(0, await final.OutboxEventos.CountAsync());
    }

    [DockerFact]
    public async Task OutboxVazio_NaoProcessaNada()
    {
        var novo = await NovoBancoAsync();
        await using var contexto = novo();

        var processados = await new OutboxProcessor(contexto, new FakeBroker(), new OutboxOptions())
            .ProcessarLoteAsync(CancellationToken.None);

        Assert.Equal(0, processados);
    }

    [DockerFact]
    public async Task EventoGravadoNaMesmaTransacaoDoLancamento_SomeJuntoNoRollback()
    {
        var novo = await NovoBancoAsync();

        await using (var contexto = novo())
        {
            await using var transacao = await contexto.Database.BeginTransactionAsync();
            var lancamento = Lancamento.Registrar(
                new NovoLancamento(Guid.NewGuid(), TipoMovimento.Credito, 10m, null, "rollback", "u"), Agora);
            await contexto.Lancamentos.AddAsync(lancamento);
            await new OutboxLancamentoEventPublisher(contexto, TimeProvider.System)
                .PublicarCriacaoAsync(lancamento, CancellationToken.None);
            await contexto.SaveChangesAsync();
            await transacao.RollbackAsync();
        }

        await using var leitura = novo();
        Assert.Equal(0, await leitura.Lancamentos.CountAsync());
        Assert.Equal(0, await leitura.OutboxEventos.CountAsync());
    }

    private sealed class FakeBroker : IBrokerPublisher
    {
        public ConcurrentBag<LancamentoCriadoEvent> Publicados { get; } = [];
        public bool Falhar { get; set; }

        public Task PublicarLoteAsync(IReadOnlyCollection<LancamentoCriadoEvent> eventos, CancellationToken cancellationToken)
        {
            if (Falhar)
                throw new InvalidOperationException("broker fora do ar");

            foreach (var e in eventos)
                Publicados.Add(e);
            return Task.CompletedTask;
        }
    }
}
