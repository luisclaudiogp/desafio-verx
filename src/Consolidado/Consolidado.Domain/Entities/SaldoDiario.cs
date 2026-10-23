using Verx.SharedKernel;

namespace Consolidado.Domain.Entities;

public sealed class SaldoDiario
{
    public Guid ContaId { get; private set; }
    public DateOnly Data { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }

    public decimal Saldo => TotalCreditos - TotalDebitos;

    private SaldoDiario() { }

    public static SaldoDiario Abrir(Guid contaId, DateOnly data) =>
        new() { ContaId = contaId, Data = data };

    public void Aplicar(Movimento movimento)
    {
        ArgumentNullException.ThrowIfNull(movimento);
        if (movimento.ContaId != ContaId || movimento.Dia != Data)
            throw new DomainException("O movimento nao pertence a este saldo diario.");

        switch (movimento.Tipo)
        {
            case TipoMovimento.Credito:
                TotalCreditos += movimento.Valor;
                break;
            case TipoMovimento.Debito:
                TotalDebitos += movimento.Valor;
                break;
            default:
                throw new DomainException("Tipo de movimento invalido.");
        }
    }
}
