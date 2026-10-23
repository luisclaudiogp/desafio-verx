namespace Lancamentos.Application.Abstractions;

public interface IVerificadorDeReplay
{
    Task<ResultadoRegistro?> BuscarAsync(
        Guid contaId, string chaveIdempotencia, string hashRequisicao, CancellationToken cancellationToken);
}
