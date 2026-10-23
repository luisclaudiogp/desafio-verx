using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Entities;
using Verx.SharedKernel;

namespace Lancamentos.Application.Commands;

public sealed class RegistrarLancamentoHandler(
    IVerificadorDeReplay replays,
    ILancamentoPersistidor persistidor,
    TimeProvider timeProvider) : ICommandHandler<RegistrarLancamentoCommand, ResultadoRegistro>
{
    public async Task<ResultadoRegistro> HandleAsync(RegistrarLancamentoCommand command, CancellationToken cancellationToken)
    {
        var dados = new NovoLancamento(
            command.ContaId, command.Tipo, command.Valor, command.OcorridoEm,
            command.ChaveIdempotencia, command.Usuario);

        var replay = await replays.BuscarAsync(
            command.ContaId, command.ChaveIdempotencia, dados.HashRequisicao, cancellationToken);
        if (replay is not null)
            return replay;

        var lancamento = Lancamento.Registrar(dados, timeProvider.GetUtcNow());

        return await persistidor.PersistirAsync(lancamento, cancellationToken)
            ?? throw new ConcorrenciaException("Conflito ao registrar o lancamento; tente novamente.");
    }
}
