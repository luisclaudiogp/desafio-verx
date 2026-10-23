using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Verx.SharedKernel;

namespace Lancamentos.Application.Commands;

public sealed class EstornarLancamentoHandler(
    ILancamentoRepository repository,
    IVerificadorDeReplay replays,
    ILancamentoPersistidor persistidor,
    TimeProvider timeProvider) : ICommandHandler<EstornarLancamentoCommand, ResultadoRegistro>
{
    public async Task<ResultadoRegistro> HandleAsync(EstornarLancamentoCommand command, CancellationToken cancellationToken)
    {
        var replay = await replays.BuscarAsync(
            command.ContaId, command.ChaveIdempotencia, HashDaRequisicao.DoEstorno(command.LancamentoId), cancellationToken);
        if (replay is not null)
            return replay;

        var original = await repository.ObterPorIdAsync(command.ContaId, command.LancamentoId, cancellationToken)
            ?? throw new NaoEncontradoException("Lancamento nao encontrado.");

        if (await repository.JaEstornadoAsync(original.Id, cancellationToken))
            throw new DomainException("O lancamento ja foi estornado.");

        var estorno = original.Estornar(command.ChaveIdempotencia, command.Usuario, timeProvider.GetUtcNow());

        return await persistidor.PersistirAsync(estorno, cancellationToken)
            ?? throw new DomainException("O lancamento ja foi estornado.");
    }
}
