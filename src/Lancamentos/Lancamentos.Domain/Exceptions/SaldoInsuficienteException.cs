using Verx.SharedKernel;

namespace Lancamentos.Domain.Exceptions;

public sealed class SaldoInsuficienteException() : DomainException("Saldo insuficiente para realizar o debito.")
{
}
