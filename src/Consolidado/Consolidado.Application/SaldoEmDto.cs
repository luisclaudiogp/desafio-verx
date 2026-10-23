namespace Consolidado.Application;

public sealed record SaldoEmDto(Guid ContaId, DateTimeOffset Em, decimal TotalCreditos, decimal TotalDebitos, decimal Saldo);
