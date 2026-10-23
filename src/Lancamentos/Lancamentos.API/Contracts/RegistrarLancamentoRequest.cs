using Verx.SharedKernel;

namespace Lancamentos.API.Contracts;

public sealed record RegistrarLancamentoRequest(TipoMovimento Tipo, decimal Valor, DateTimeOffset? OcorridoEm);
