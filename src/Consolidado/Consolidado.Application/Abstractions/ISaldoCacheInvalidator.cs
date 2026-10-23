namespace Consolidado.Application.Abstractions;

public interface ISaldoCacheInvalidator
{
    Task InvalidarSaldoDiarioAsync(Guid contaId, DateOnly data, CancellationToken cancellationToken);
}
