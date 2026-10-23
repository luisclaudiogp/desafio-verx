using Verx.SharedKernel;

namespace Shared.Contracts;

public sealed record LancamentoCriadoEvent(
    Guid LancamentoId,
    Guid ContaId,
    TipoMovimento Tipo,
    decimal Valor,
    DateTimeOffset OcorridoEm,
    DateTimeOffset RegistradoEm);
