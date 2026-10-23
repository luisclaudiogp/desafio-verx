using Verx.SharedKernel;

namespace Lancamentos.Application.Queries;

public sealed record ObterLancamentoQuery(Guid ContaId, Guid LancamentoId) : IQuery<LancamentoDto>;
