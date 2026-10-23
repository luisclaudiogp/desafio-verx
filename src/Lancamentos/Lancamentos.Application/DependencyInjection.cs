using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Commands;
using Lancamentos.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Verx.SharedKernel;

namespace Lancamentos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddLancamentosApplication(this IServiceCollection services)
    {
        services.AddScoped<IVerificadorDeReplay, VerificadorDeReplay>();
        services.AddScoped<ILancamentoPersistidor, LancamentoPersistidor>();
        services.AddScoped<ICommandHandler<RegistrarLancamentoCommand, ResultadoRegistro>, RegistrarLancamentoHandler>();
        services.AddScoped<ICommandHandler<EstornarLancamentoCommand, ResultadoRegistro>, EstornarLancamentoHandler>();
        services.AddScoped<IQueryHandler<ObterLancamentoQuery, LancamentoDto>, ObterLancamentoHandler>();
        return services;
    }
}
