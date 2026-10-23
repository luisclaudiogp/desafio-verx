using Consolidado.Application;
using Consolidado.Application.Abstractions;
using Consolidado.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure.Projecao;

public sealed class ProjecaoRepository(ConsolidadoDbContext context, ProjecaoOptions options) : IProjecaoRepository
{
    public async Task<IProjecaoTransacao> IniciarAsync(CancellationToken cancellationToken)
    {
        var transacao = await context.Database.BeginTransactionAsync(cancellationToken);
        return options.Modo == ModoDeProjecao.Sql
            ? new SqlProjecaoTransacao(context, transacao)
            : new EfProjecaoTransacao(context, transacao);
    }

    public async Task RegistrarRejeicoesAsync(IReadOnlyCollection<RejeicaoMovimento> rejeicoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rejeicoes);

        context.EventosRejeitados.AddRange(
            rejeicoes.Select(r => new EventoRejeitado(r.LancamentoId, r.Payload, r.Motivo, r.RejeitadoEm)));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }
}
