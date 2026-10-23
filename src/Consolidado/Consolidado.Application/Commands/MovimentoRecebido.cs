using Verx.SharedKernel;

namespace Consolidado.Application.Commands;

public sealed record MovimentoRecebido(
    Guid LancamentoId,
    Guid ContaId,
    TipoMovimento Tipo,
    decimal Valor,
    DateTimeOffset OcorridoEm);
