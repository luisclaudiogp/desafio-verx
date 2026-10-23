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

    /// <summary>Agrega movimentos em um delta por (conta, dia): a regra de totalizacao pertence ao dominio.</summary>
    public static IReadOnlyList<SaldoDiario> AcumularPorDia(IEnumerable<Movimento> movimentos)
    {
        ArgumentNullException.ThrowIfNull(movimentos);

        var deltas = new List<SaldoDiario>();
        foreach (var grupo in movimentos.GroupBy(m => (m.ContaId, m.Dia)))
        {
            var delta = Abrir(grupo.Key.ContaId, grupo.Key.Dia);
            foreach (var movimento in grupo)
                delta.Aplicar(movimento);
            deltas.Add(delta);
        }

        return deltas;
    }

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
