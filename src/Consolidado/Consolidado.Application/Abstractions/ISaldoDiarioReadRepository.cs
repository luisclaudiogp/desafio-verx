namespace Consolidado.Application.Abstractions;

public interface ISaldoDiarioReadRepository
{
    Task<SaldoDiarioDto> ObterSaldoDiarioAsync(Guid contaId, DateOnly data, CancellationToken cancellationToken);
}
