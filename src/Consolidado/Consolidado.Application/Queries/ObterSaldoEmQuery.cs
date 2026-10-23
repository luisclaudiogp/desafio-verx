using Verx.SharedKernel;

namespace Consolidado.Application.Queries;

public sealed record ObterSaldoEmQuery(Guid ContaId, DateTimeOffset Em);
