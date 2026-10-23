using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Verx.Web.Hosting;

public static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabaseAsync<TContext>(this WebApplication app, int maxTentativas = 8)
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

        for (var tentativa = 1; tentativa < maxTentativas; tentativa++)
        {
            try
            {
                await context.Database.MigrateAsync();
                return;
            }
            catch (Exception ex)
            {
                var espera = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, tentativa)));
                logger.LogWarning(ex, "Banco indisponivel (tentativa {Tentativa}/{Max}). Nova tentativa em {Espera}s.",
                    tentativa, maxTentativas, espera.TotalSeconds);
                await Task.Delay(espera);
            }
        }

        // ultima tentativa: a falha, se ocorrer, sobe para quem chamou
        await context.Database.MigrateAsync();
    }
}
