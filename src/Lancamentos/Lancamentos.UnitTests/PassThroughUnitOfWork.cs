using Verx.SharedKernel;

namespace Lancamentos.UnitTests;

internal sealed class PassThroughUnitOfWork : IUnitOfWork
{
    public int Execucoes { get; private set; }

    public Exception? FalhaAoConfirmar { get; set; }

    public int QuantidadeDeFalhas { get; set; } = int.MaxValue;

    public async Task<T> ExecutarAsync<T>(Func<Task<T>> operacao, CancellationToken cancellationToken)
    {
        Execucoes++;
        var resultado = await operacao();
        if (FalhaAoConfirmar is not null && Execucoes <= QuantidadeDeFalhas)
            throw FalhaAoConfirmar;
        return resultado;
    }
}
