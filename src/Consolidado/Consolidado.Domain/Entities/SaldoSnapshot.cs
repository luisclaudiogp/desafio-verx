using Verx.SharedKernel;

namespace Consolidado.Domain.Entities;

public sealed class SaldoSnapshot
{
    public Guid ContaId { get; private set; }
    public DateOnly Data { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public DateTimeOffset GeradoEm { get; private set; }

    public decimal Saldo => TotalCreditos - TotalDebitos;

    private SaldoSnapshot() { }

    public static SaldoSnapshot Criar(
        Guid contaId, DateOnly data, decimal totalCreditos, decimal totalDebitos, DateTimeOffset geradoEm)
    {
        if (contaId == Guid.Empty)
            throw new DomainException("A conta do snapshot e obrigatoria.");
        if (totalCreditos < 0 || totalDebitos < 0)
            throw new DomainException("Os totais acumulados do snapshot nao podem ser negativos.");

        return new SaldoSnapshot
        {
            ContaId = contaId,
            Data = data,
            TotalCreditos = totalCreditos,
            TotalDebitos = totalDebitos,
            GeradoEm = geradoEm.ToUniversalTime()
        };
    }
}
