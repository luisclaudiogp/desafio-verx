using Verx.SharedKernel;

namespace Lancamentos.Application.Commands;

public sealed record EstornarLancamentoCommand(
    Guid ContaId,
    Guid LancamentoId,
    string ChaveIdempotencia,
    string Usuario);
