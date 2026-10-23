using Consolidado.Application.Commands;
using Consolidado.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Verx.SharedKernel;

namespace Consolidado.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddConsolidadoApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<RegistrarMovimentosEmLoteCommand, ResultadoProjecao>, RegistrarMovimentosEmLoteHandler>();
        services.AddScoped<ICommandHandler<GerarSnapshotsCommand, ResultadoSnapshots>, GerarSnapshotsHandler>();
        services.AddScoped<IQueryHandler<ObterSaldoDiarioQuery, SaldoDiarioDto>, ObterSaldoDiarioHandler>();
        services.AddScoped<IQueryHandler<ObterSaldoEmQuery, SaldoEmDto>, ObterSaldoEmHandler>();
        return services;
    }
}
