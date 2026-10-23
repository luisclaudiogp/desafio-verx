using System.Text.Json.Serialization;
using Verx.SharedKernel;

namespace Lancamentos.API.Contracts;

public sealed record RegistrarLancamentoRequest(
    [property: JsonRequired] TipoMovimento Tipo,
    [property: JsonRequired] decimal Valor,
    DateTimeOffset? OcorridoEm);
