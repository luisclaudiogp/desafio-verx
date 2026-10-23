using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lancamentos.Infrastructure.Outbox;

public sealed class OutboxRelay(
    IServiceScopeFactory scopeFactory,
    OutboxOptions options,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, Math.Max(1, options.Paralelismo))
            .Select(_ => Task.Run(() => ExecutarWorkerAsync(stoppingToken), stoppingToken)));

    private async Task ExecutarWorkerAsync(CancellationToken stoppingToken)
    {
        var falhasSeguidas = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                var processados = await processor.ProcessarLoteAsync(stoppingToken);

                falhasSeguidas = 0;
                if (processados == 0)
                    await Task.Delay(options.PollOciosoMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                falhasSeguidas++;
                OutboxMetrics.Falhas.Add(1);
                var espera = Math.Min(options.BackoffMaximoMs, 100 * (int)Math.Pow(2, Math.Min(falhasSeguidas, 10)));
                logger.LogWarning(ex, "Falha ao entregar o outbox (tentativa {Tentativa}); nova tentativa em {Espera} ms.",
                    falhasSeguidas, espera);
                await Task.Delay(espera, stoppingToken);
            }
        }
    }
}
