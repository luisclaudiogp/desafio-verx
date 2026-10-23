using Verx.SharedKernel;

namespace Lancamentos.Application.Commands;

public sealed record RegistrarLancamentoCommand(
    Guid ContaId,
    TipoMovimento Tipo,
    decimal Valor,
    DateTimeOffset? OcorridoEm,
    string ChaveIdempotencia,
    string Usuario) : ICommand<ResultadoRegistro>;
