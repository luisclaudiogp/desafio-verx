namespace Consolidado.Application;

public sealed record SaldoDiarioDto(Guid ContaId, DateOnly Data, decimal TotalCreditos, decimal TotalDebitos, decimal Saldo);
