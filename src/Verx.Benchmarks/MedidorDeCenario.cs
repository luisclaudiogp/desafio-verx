using System.Diagnostics;
using Consolidado.Domain.Entities;

namespace Verx.Benchmarks;

public sealed class MedidorDeCenario(int repeticoes, int aquecimentos)
{
    private sealed class Estado(IEstrategiaDeProjecao estrategia, int contas)
    {
        public IEstrategiaDeProjecao Estrategia { get; } = estrategia;
        public GeradorDeMovimentos Gerador { get; } = new(semente: 42);
        public List<Guid> ContasFixas { get; } = GeradorDeMovimentos.NovasContas(contas);
        public IReadOnlyList<Movimento> Anterior { get; set; } = [];
        public List<double> Tempos { get; } = [];
    }

    public async Task<List<ResultadoDeCenario>> MedirAsync(IReadOnlyList<IEstrategiaDeProjecao> estrategias, CenarioDeCarga cenario)
    {
        var estados = estrategias.Select(e => new Estado(e, cenario.Contas)).ToList();

        for (var i = 0; i < aquecimentos + repeticoes; i++)
        {
            var cronometrar = i >= aquecimentos;
            foreach (var estado in Rotacionar(estados, i))
                await ExecutarUmaVezAsync(estado, cenario, cronometrar);
        }

        return estados.Select(e => Resumir(e, cenario)).ToList();
    }

    private static IEnumerable<Estado> Rotacionar(List<Estado> estados, int rodada) =>
        estados.Skip(rodada % estados.Count).Concat(estados.Take(rodada % estados.Count));

    private static async Task ExecutarUmaVezAsync(Estado estado, CenarioDeCarga cenario, bool cronometrar)
    {
        var contas = cenario.ContasNovas ? GeradorDeMovimentos.NovasContas(cenario.Contas) : estado.ContasFixas;
        var lote = estado.Gerador.Lote(contas, cenario.Eventos, estado.Anterior, cenario.FracaoDuplicada);

        var relogio = Stopwatch.StartNew();
        await estado.Estrategia.AplicarAsync(lote, CancellationToken.None);
        relogio.Stop();

        if (cronometrar)
            estado.Tempos.Add(relogio.Elapsed.TotalMilliseconds);

        estado.Anterior = lote;
    }

    private static ResultadoDeCenario Resumir(Estado estado, CenarioDeCarga cenario)
    {
        var tempos = estado.Tempos.Order().ToList();
        return new ResultadoDeCenario(
            estado.Estrategia.Nome,
            cenario.Eventos,
            tempos[tempos.Count / 2],
            tempos[(int)Math.Ceiling(tempos.Count * 0.95) - 1],
            tempos[0]);
    }
}
