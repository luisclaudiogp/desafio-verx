using Consolidado.Application.Commands;
using Consolidado.Domain.Entities;
using Consolidado.Infrastructure.Data;
using Consolidado.Infrastructure.Projecao;
using Consolidado.Infrastructure.Snapshots;

namespace Verx.Benchmarks;

public sealed class ProjecaoPeloHandlerEstrategia(Func<ConsolidadoDbContext> fabrica, ModoDeProjecao modo, string nome) : IEstrategiaDeProjecao
{
    private long _chamadas;
    private long _tentativas;

    public string Nome => nome;

    public long Chamadas => Interlocked.Read(ref _chamadas);

    public long Tentativas => Interlocked.Read(ref _tentativas);

    public async Task AplicarAsync(IReadOnlyList<Movimento> lote, CancellationToken cancellationToken)
    {
        await using var contexto = fabrica();
        var repositorio = new ProjecaoRepositoryContador(new ProjecaoRepository(contexto, new ProjecaoOptions { Modo = modo }));
        var handler = new RegistrarMovimentosEmLoteHandler(
            repositorio, new CorretorDeSnapshots(contexto), new InvalidadorNulo(), TimeProvider.System);
        var itens = lote.Select(m => new MovimentoRecebido(m.LancamentoId, m.ContaId, m.Tipo, m.Valor, m.OcorridoEm)).ToList();

        Interlocked.Increment(ref _chamadas);
        try
        {
            await handler.HandleAsync(new RegistrarMovimentosEmLoteCommand(itens), cancellationToken);
        }
        finally
        {
            Interlocked.Add(ref _tentativas, repositorio.Inicios);
        }
    }
}
