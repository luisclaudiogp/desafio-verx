using Verx.SharedKernel;

namespace Lancamentos.Domain.Entities;

public sealed record NovoLancamento(
    Guid ContaId,
    TipoMovimento Tipo,
    decimal Valor,
    DateTimeOffset? OcorridoEm,
    string ChaveIdempotencia,
    string RegistradoPor)
{
    public string HashRequisicao => HashDaRequisicao.DoRegistro(Tipo, Valor, OcorridoEm);
}
