namespace Verx.SharedKernel;

public interface IUnitOfWork
{
    Task<T> ExecutarAsync<T>(Func<Task<T>> operacao, CancellationToken cancellationToken);
}
