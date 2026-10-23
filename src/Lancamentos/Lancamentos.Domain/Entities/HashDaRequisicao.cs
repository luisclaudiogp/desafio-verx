using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Verx.SharedKernel;

namespace Lancamentos.Domain.Entities;

public static class HashDaRequisicao
{
    public static string DoRegistro(TipoMovimento tipo, decimal valor, DateTimeOffset? ocorridoEm) =>
        Calcular(string.Join(
            '|',
            "registro",
            ((short)tipo).ToString(CultureInfo.InvariantCulture),
            valor.ToString("F2", CultureInfo.InvariantCulture),
            ocorridoEm?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "-"));

    public static string DoEstorno(Guid lancamentoId) =>
        Calcular("estorno|" + lancamentoId.ToString("N"));

    private static string Calcular(string conteudo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo)));
}
