using Consolidado.Domain.Entities;
using Verx.SharedKernel;

namespace Verx.Benchmarks;

public sealed class GeradorDeMovimentos(int semente)
{
    private static readonly DateTimeOffset Dia = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Random _aleatorio = new(semente);

    public List<Movimento> Lote(IReadOnlyList<Guid> contas, int eventos, IReadOnlyList<Movimento>? reenvio = null, double fracaoDuplicada = 0)
    {
        var lote = new List<Movimento>(eventos);

        var duplicados = reenvio is null ? 0 : (int)(eventos * fracaoDuplicada);
        if (reenvio is not null)
            lote.AddRange(reenvio.Take(duplicados));

        while (lote.Count < eventos)
            lote.Add(Novo(contas[_aleatorio.Next(contas.Count)]));

        return lote;
    }

    public static List<Guid> NovasContas(int quantidade) =>
        Enumerable.Range(0, quantidade).Select(_ => Guid.NewGuid()).ToList();

    public Movimento Novo(Guid conta) =>
        Movimento.Criar(
            Guid.NewGuid(),
            conta,
            _aleatorio.Next(3) == 0 ? TipoMovimento.Debito : TipoMovimento.Credito,
            _aleatorio.Next(1, 50000) / 100m,
            Dia.AddMinutes(_aleatorio.Next(0, 600)),
            Dia);
}
