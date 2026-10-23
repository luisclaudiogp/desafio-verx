using Microsoft.AspNetCore.Http;

namespace Verx.Web.Http;

public static class IdempotencyKey
{
    public const string Header = "Idempotency-Key";

    public static string Obter(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var valor = request.Headers[Header].ToString();
        if (string.IsNullOrWhiteSpace(valor))
            throw new RequisicaoInvalidaException($"O header '{Header}' e obrigatorio.");
        return valor.Trim();
    }
}
