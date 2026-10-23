using Microsoft.EntityFrameworkCore;
using Npgsql;
using Verx.SharedKernel;

namespace Verx.Persistence;

public sealed class EfUnitOfWork<TContext>(TContext context) : IUnitOfWork where TContext : DbContext
{
    public async Task<T> ExecutarAsync<T>(Func<Task<T>> operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var resultado = await operacao();
            await context.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return resultado;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            context.ChangeTracker.Clear();
            throw new ConcorrenciaException("Conflito de concorrencia otimista.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.ChangeTracker.Clear();
            throw new ChaveDuplicadaException("Violacao de unicidade.", ex);
        }
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
