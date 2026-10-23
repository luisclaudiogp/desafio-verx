using Verx.SharedKernel;

namespace Consolidado.Application.Queries;

public sealed record ObterSaldoDiarioQuery(Guid ContaId, DateOnly Data);
