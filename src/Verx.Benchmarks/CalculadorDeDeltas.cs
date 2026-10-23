using Consolidado.Domain.Entities;

namespace Verx.Benchmarks;

public static class CalculadorDeDeltas
{
    public static List<SaldoDiario> De(IReadOnlyList<Movimento> movimentos)
    {
        var deltas = new List<SaldoDiario>();

        foreach (var grupo in movimentos.GroupBy(m => (m.ContaId, m.Dia)).OrderBy(g => g.Key.ContaId).ThenBy(g => g.Key.Dia))
        {
            var delta = SaldoDiario.Abrir(grupo.Key.ContaId, grupo.Key.Dia);
            foreach (var movimento in grupo)
                delta.Aplicar(movimento);
            deltas.Add(delta);
        }

        return deltas;
    }
}
