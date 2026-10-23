using Npgsql;

namespace Consolidado.Infrastructure.Projecao;

internal static class ConflitoTransitorio
{
    private static readonly string[] Estados =
    [
        PostgresErrorCodes.UniqueViolation,
        PostgresErrorCodes.DeadlockDetected,
        PostgresErrorCodes.SerializationFailure
    ];

    public static bool Eh(Exception excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException!)
        {
            if (atual is PostgresException postgres && Estados.Contains(postgres.SqlState))
                return true;

            if (atual.InnerException is null)
                return false;
        }

        return false;
    }
}
