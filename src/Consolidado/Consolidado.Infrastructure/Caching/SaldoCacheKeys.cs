namespace Consolidado.Infrastructure.Caching;

internal static class SaldoCacheKeys
{
    public static string Diario(Guid contaId, DateOnly data) => $"saldo:{contaId:N}:{data:yyyy-MM-dd}";
}
