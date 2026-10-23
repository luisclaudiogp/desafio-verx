using Consolidado.Domain.Tempo;
using Verx.SharedKernel;

namespace Consolidado.Domain.Entities;

public sealed class Movimento
{
    public Guid LancamentoId { get; private set; }
    public Guid ContaId { get; private set; }
    public TipoMovimento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public DateTimeOffset OcorridoEm { get; private set; }
    public DateTimeOffset ProcessadoEm { get; private set; }

    private Movimento() { }

    public static Movimento Criar(
        Guid lancamentoId, Guid contaId, TipoMovimento tipo, decimal valor, DateTimeOffset ocorridoEm, DateTimeOffset agora)
    {
        if (lancamentoId == Guid.Empty || contaId == Guid.Empty)
            throw new DomainException("Identificadores do movimento sao obrigatorios.");
        if (!Enum.IsDefined(tipo))
            throw new DomainException("Tipo de movimento invalido.");

        return new Movimento
        {
            LancamentoId = lancamentoId,
            ContaId = contaId,
            Tipo = tipo,
            Valor = Dinheiro.Criar(valor).Valor,
            OcorridoEm = ocorridoEm.ToUniversalTime(),
            ProcessadoEm = agora.ToUniversalTime()
        };
    }

    public DateOnly Dia => OcorridoEm.DiaUtc();
}
