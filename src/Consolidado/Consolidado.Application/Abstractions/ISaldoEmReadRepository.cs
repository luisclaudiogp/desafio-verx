namespace Consolidado.Application.Abstractions;

public interface ISaldoEmReadRepository
{
    Task<SaldoEmDto> ObterSaldoEmAsync(Guid contaId, DateTimeOffset em, CancellationToken cancellationToken);
}
