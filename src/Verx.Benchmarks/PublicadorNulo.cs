using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Entities;

namespace Verx.Benchmarks;

public sealed class PublicadorNulo : ILancamentoEventPublisher
{
    public Task PublicarCriacaoAsync(Lancamento lancamento, CancellationToken cancellationToken) => Task.CompletedTask;
}
