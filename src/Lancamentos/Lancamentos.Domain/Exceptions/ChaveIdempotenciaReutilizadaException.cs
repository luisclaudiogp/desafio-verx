using Verx.SharedKernel;

namespace Lancamentos.Domain.Exceptions;

public sealed class ChaveIdempotenciaReutilizadaException()
    : DomainException("A Idempotency-Key ja foi utilizada em uma requisicao diferente.")
{
}
