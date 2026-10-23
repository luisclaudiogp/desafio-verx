namespace Verx.Benchmarks;

public static class AquecedorDeBanco
{
    public static async Task AquecerAsync(IEstrategiaDeProjecao estrategia, int lotes)
    {
        var gerador = new GeradorDeMovimentos(semente: 7);
        var contas = GeradorDeMovimentos.NovasContas(50);

        for (var i = 0; i < lotes; i++)
            await estrategia.AplicarAsync(gerador.Lote(contas, 500), CancellationToken.None);
    }
}
