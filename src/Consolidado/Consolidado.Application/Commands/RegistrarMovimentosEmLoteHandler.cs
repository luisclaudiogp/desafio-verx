using System.Text.Json;
using Consolidado.Application.Abstractions;
using Consolidado.Domain.Entities;
using Verx.SharedKernel;

namespace Consolidado.Application.Commands;

public sealed class RegistrarMovimentosEmLoteHandler(
    IProjecaoRepository projecao,
    ICorretorDeSnapshots snapshots,
    ISaldoCacheInvalidator cache,
    TimeProvider timeProvider) : ICommandHandler<RegistrarMovimentosEmLoteCommand, ResultadoProjecao>
{
    private const int MaximoDeTentativasPorConflito = 3;

    public async Task<ResultadoProjecao> HandleAsync(RegistrarMovimentosEmLoteCommand command, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow();
        var (validos, rejeicoes) = Separar(command.Itens, agora);

        if (rejeicoes.Count > 0)
            await projecao.RegistrarRejeicoesAsync(rejeicoes, cancellationToken);

        if (validos.Count == 0)
            return new ResultadoProjecao(0, 0, rejeicoes.Count);

        var aplicados = await AplicarAsync(validos, cancellationToken);
        await InvalidarCacheAsync(aplicados, cancellationToken);

        return new ResultadoProjecao(aplicados.Count, validos.Count - aplicados.Count, rejeicoes.Count);
    }

    private static (List<Movimento> Validos, List<RejeicaoMovimento> Rejeicoes) Separar(
        IReadOnlyList<MovimentoRecebido> itens, DateTimeOffset agora)
    {
        var validos = new List<Movimento>(itens.Count);
        var rejeicoes = new List<RejeicaoMovimento>();

        foreach (var item in itens.DistinctBy(i => i.LancamentoId))
        {
            try
            {
                validos.Add(Movimento.Criar(item.LancamentoId, item.ContaId, item.Tipo, item.Valor, item.OcorridoEm, agora));
            }
            catch (DomainException ex)
            {
                rejeicoes.Add(new RejeicaoMovimento(item.LancamentoId, JsonSerializer.Serialize(item), ex.Message, agora));
            }
        }

        return (validos, rejeicoes);
    }

    private async Task<IReadOnlyList<Movimento>> AplicarAsync(List<Movimento> validos, CancellationToken cancellationToken)
    {
        for (var tentativa = 1; tentativa < MaximoDeTentativasPorConflito; tentativa++)
        {
            try
            {
                return await TentarAplicarAsync(validos, cancellationToken);
            }
            catch (ConcorrenciaException)
            {
                // conflito de concorrencia: a transacao foi desfeita, repete com os saldos atualizados
            }
        }

        // ultima tentativa: o conflito, se persistir, sobe para o retry da mensageria
        return await TentarAplicarAsync(validos, cancellationToken);
    }

    private async Task<IReadOnlyList<Movimento>> TentarAplicarAsync(List<Movimento> validos, CancellationToken cancellationToken)
    {
        await using var transacao = await projecao.IniciarAsync(cancellationToken);

        var aplicados = await transacao.InserirMovimentosAsync(validos, cancellationToken);
        var deltas = SaldoDiario.AcumularPorDia(aplicados);
        await transacao.AcumularSaldosAsync(deltas, cancellationToken);
        await snapshots.CorrigirAsync(deltas, cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return aplicados;
    }

    private Task InvalidarCacheAsync(IReadOnlyList<Movimento> aplicados, CancellationToken cancellationToken) =>
        Task.WhenAll(aplicados
            .Select(m => (m.ContaId, m.Dia))
            .Distinct()
            .Select(k => cache.InvalidarSaldoDiarioAsync(k.ContaId, k.Dia, cancellationToken)));
}
