using Consolidado.Domain.Entities;
using Npgsql;

namespace Consolidado.Infrastructure.Projecao;

internal static class DeltasSql
{
    public static NpgsqlParameter[] Parametros(IReadOnlyCollection<SaldoDiario> deltas)
    {
        var ordenados = deltas.OrderBy(d => d.ContaId).ThenBy(d => d.Data).ToList();
        return
        [
            new NpgsqlParameter<Guid[]>("contas", ordenados.Select(d => d.ContaId).ToArray()),
            new NpgsqlParameter<DateOnly[]>("datas", ordenados.Select(d => d.Data).ToArray()),
            new NpgsqlParameter<decimal[]>("creditos", ordenados.Select(d => d.TotalCreditos).ToArray()),
            new NpgsqlParameter<decimal[]>("debitos", ordenados.Select(d => d.TotalDebitos).ToArray())
        ];
    }
}
