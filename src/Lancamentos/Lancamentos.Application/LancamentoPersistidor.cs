using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Verx.SharedKernel;

namespace Lancamentos.Application;

public sealed class LancamentoPersistidor(
    ILancamentoRepository repository,
    IPosicaoContaRepository posicoes,
    ILancamentoEventPublisher publisher,
    IUnitOfWork unitOfWork,
    IVerificadorDeReplay replays,
    TimeProvider timeProvider) : ILancamentoPersistidor
{
    private const int MaximoDeTentativas = 5;
    private const int EsperaBaseEmMilissegundos = 3;

    public async Task<ResultadoRegistro?> PersistirAsync(Lancamento lancamento, CancellationToken cancellationToken)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                var resultado = await TentarAsync(lancamento, cancellationToken);
                if (resultado is not null || tentativa == MaximoDeTentativas)
                    return resultado;
            }
            catch (ConcorrenciaException) when (tentativa < MaximoDeTentativas)
            {
                await EsperarAsync(tentativa, cancellationToken);
                continue;
            }

            await EsperarAsync(tentativa, cancellationToken);
        }
    }

    private async Task<ResultadoRegistro?> TentarAsync(Lancamento lancamento, CancellationToken cancellationToken)
    {
        try
        {
            return await unitOfWork.ExecutarAsync(
                async () =>
                {
                    var posicao = await posicoes.ObterParaAtualizacaoAsync(lancamento.ContaId, cancellationToken);
                    posicao.Aplicar(lancamento);
                    await repository.AdicionarAsync(lancamento, cancellationToken);
                    await publisher.PublicarCriacaoAsync(lancamento, cancellationToken);
                    return new ResultadoRegistro(LancamentoDto.De(lancamento), Replay: false);
                },
                cancellationToken);
        }
        catch (ChaveDuplicadaException)
        {
            return await replays.BuscarAsync(
                lancamento.ContaId, lancamento.ChaveIdempotencia, lancamento.HashRequisicao, cancellationToken);
        }
    }

    private Task EsperarAsync(int tentativa, CancellationToken cancellationToken) =>
        Task.Delay(
            TimeSpan.FromMilliseconds(Random.Shared.Next(1, EsperaBaseEmMilissegundos + 1) * tentativa),
            timeProvider,
            cancellationToken);
}
