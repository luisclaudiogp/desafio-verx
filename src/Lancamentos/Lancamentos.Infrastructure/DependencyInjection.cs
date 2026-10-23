using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure.Data;
using Lancamentos.Infrastructure.Outbox;
using Lancamentos.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Verx.Persistence;
using Verx.SharedKernel;

namespace Lancamentos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLancamentosInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddPersistence(configuration)
            .AddOutbox(configuration)
            .AddBroker(configuration);
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LancamentosDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ILancamentoRepository, LancamentoRepository>();
        var posicao = configuration.GetSection(PosicaoOptions.Section).Get<PosicaoOptions>() ?? new PosicaoOptions();
        if (posicao.Modo == ModoDePosicao.Sql)
            services.AddScoped<IPosicaoContaRepository, SqlPosicaoContaRepository>();
        else
            services.AddScoped<IPosicaoContaRepository, EfPosicaoContaRepository>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork<LancamentosDbContext>>();
        return services;
    }

    private static IServiceCollection AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        var opcoes = configuration.GetSection(OutboxOptions.Section).Get<OutboxOptions>() ?? new OutboxOptions();

        services.AddSingleton(opcoes);
        services.AddScoped<ILancamentoEventPublisher, OutboxLancamentoEventPublisher>();
        services.AddScoped<OutboxProcessor>();
        services.AddSingleton<IBrokerPublisher, MassTransitBrokerPublisher>();
        services.AddHostedService<OutboxRelay>();
        return services;
    }

    private static IServiceCollection AddBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbit = configuration.GetSection("RabbitMQ");
                cfg.Host(rabbit["Host"], "/", h =>
                {
                    h.Username(rabbit["Username"]!);
                    h.Password(rabbit["Password"]!);
                });
            });
        });

        return services;
    }
}
