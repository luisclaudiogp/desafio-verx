using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Exceptions;
using Lancamentos.Domain.Repositories;

namespace Lancamentos.Application;

public sealed class VerificadorDeReplay(ILancamentoRepository repository) : IVerificadorDeReplay
{
    public async Task<ResultadoRegistro?> BuscarAsync(
        Guid contaId, string chaveIdempotencia, string hashRequisicao, CancellationToken cancellationToken)
    {
        var existente = await repository.ObterPorChaveAsync(contaId, chaveIdempotencia, cancellationToken);
        if (existente is null)
            return null;

        if (!existente.CorrespondeAoHash(hashRequisicao))
            throw new ChaveIdempotenciaReutilizadaException();

        return new ResultadoRegistro(LancamentoDto.De(existente), Replay: true);
    }
}
