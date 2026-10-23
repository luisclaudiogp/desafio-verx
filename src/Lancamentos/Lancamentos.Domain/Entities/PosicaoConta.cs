using Lancamentos.Domain.Exceptions;
using Verx.SharedKernel;

namespace Lancamentos.Domain.Entities;

public sealed class PosicaoConta
{
    public Guid ContaId { get; private set; }
    public decimal Saldo { get; private set; }

    private PosicaoConta() { }

    public static PosicaoConta Abrir(Guid contaId)
    {
        if (contaId == Guid.Empty)
            throw new DomainException("A conta e obrigatoria.");

        return new PosicaoConta { ContaId = contaId };
    }

    public void Aplicar(Lancamento lancamento)
    {
        ArgumentNullException.ThrowIfNull(lancamento);

        if (lancamento.ContaId != ContaId)
            throw new DomainException("O lancamento pertence a outra conta.");

        var valor = lancamento.Valor.Valor;
        if (lancamento.Tipo == TipoMovimento.Credito)
        {
            Saldo += valor;
            return;
        }

        if (lancamento.EstornoDeId is null && Saldo < valor)
            throw new SaldoInsuficienteException();

        Saldo -= valor;
    }
}
