using Consolidado.Application;
using Consolidado.Application.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Verx.SharedKernel;

namespace Consolidado.Infrastructure.Snapshots;

public sealed class SnapshotWorker(
    IServiceScopeFactory scopeFactory,
    SnapshotOptions options,
    ILogger<SnapshotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Habilitado)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<GerarSnapshotsCommand, ResultadoSnapshots>>();
                var resultado = await handler.HandleAsync(
                    new GerarSnapshotsCommand(options.MargemDias, options.LoteContas), stoppingToken);

                if (resultado.Gerados > 0)
                    logger.LogInformation("{Gerados} snapshots de saldo gerados em {Lotes} lotes.", resultado.Gerados, resultado.Lotes);

                await Task.Delay(TimeSpan.FromSeconds(options.IntervaloSegundos), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao gerar snapshots de saldo; nova tentativa em {Segundos} s.", options.RetentativaSegundos);
                await Task.Delay(TimeSpan.FromSeconds(options.RetentativaSegundos), stoppingToken);
            }
        }
    }
}
