using Consolidado.Application.Abstractions;

namespace Verx.Benchmarks;

public sealed class InvalidadorNulo : ISaldoCacheInvalidator
{
    public Task InvalidarSaldoDiarioAsync(Guid contaId, DateOnly data, CancellationToken cancellationToken) => Task.CompletedTask;
}
