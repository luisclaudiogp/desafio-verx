using Lancamentos.Domain.Entities;
using Verx.SharedKernel;

namespace Lancamentos.Application;

public sealed record LancamentoDto(
    Guid Id,
    Guid ContaId,
    TipoMovimento Tipo,
    decimal Valor,
    DateTimeOffset OcorridoEm,
    DateTimeOffset RegistradoEm,
    Guid? EstornoDeId)
{
    public static LancamentoDto De(Lancamento l) =>
        new(l.Id, l.ContaId, l.Tipo, l.Valor.Valor, l.OcorridoEm, l.RegistradoEm, l.EstornoDeId);
}
