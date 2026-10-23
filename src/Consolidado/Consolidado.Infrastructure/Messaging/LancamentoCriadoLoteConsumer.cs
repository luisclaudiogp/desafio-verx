using Consolidado.Application.Commands;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts;
using Verx.SharedKernel;

namespace Consolidado.Infrastructure.Messaging;

public sealed class LancamentoCriadoLoteConsumer(
    ICommandHandler<RegistrarMovimentosEmLoteCommand, Consolidado.Application.ResultadoProjecao> handler,
    ILogger<LancamentoCriadoLoteConsumer> logger) : IConsumer<Batch<LancamentoCriadoEvent>>
{
    public async Task Consume(ConsumeContext<Batch<LancamentoCriadoEvent>> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var itens = context.Message
            .Select(m => m.Message)
            .Select(e => new MovimentoRecebido(e.LancamentoId, e.ContaId, e.Tipo, e.Valor, e.OcorridoEm))
            .ToList();

        var resultado = await handler.HandleAsync(new RegistrarMovimentosEmLoteCommand(itens), context.CancellationToken);

        if (resultado.Duplicados > 0 || resultado.Rejeitados > 0)
            logger.LogWarning(
                "Lote processado: {Aplicados} aplicados, {Duplicados} duplicados ignorados, {Rejeitados} rejeitados.",
                resultado.Aplicados, resultado.Duplicados, resultado.Rejeitados);
    }
}
