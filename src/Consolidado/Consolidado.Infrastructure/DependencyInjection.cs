using Consolidado.Application.Abstractions;
using Consolidado.Infrastructure.Caching;
using Consolidado.Infrastructure.Data;
using Consolidado.Infrastructure.Messaging;
using Consolidado.Infrastructure.Projecao;
using Consolidado.Infrastructure.Repositories;
using Consolidado.Infrastructure.Snapshots;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Consolidado.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddConsolidadoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddSnapshots(configuration)
            .AddPersistence(configuration)
            .AddCaching(configuration)
            .AddMessaging(configuration);
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ConsolidadoDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddSingleton(configuration.GetSection(ProjecaoOptions.Section).Get<ProjecaoOptions>() ?? new ProjecaoOptions());
        services.AddScoped<IProjecaoRepository, ProjecaoRepository>();
        services.AddScoped<ISnapshotRepository, SnapshotRepository>();
        services.AddScoped<ICorretorDeSnapshots, CorretorDeSnapshots>();
        services.AddScoped<SaldoReadRepository>();
        services.AddScoped<ISaldoEmReadRepository>(sp => sp.GetRequiredService<SaldoReadRepository>());
        return services;
    }

    private static IServiceCollection AddSnapshots(this IServiceCollection services, IConfiguration configuration)
    {
        var opcoes = configuration.GetSection(SnapshotOptions.Section).Get<SnapshotOptions>() ?? new SnapshotOptions();

        services.AddSingleton(opcoes);
        services.AddHostedService<SnapshotWorker>();
        return services;
    }

    private static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:ConnectionString"];
            options.InstanceName = "consolidado:";
        });

        services.AddSingleton(FailOpenCache.CriarPipelinePadrao());
        services.AddSingleton<IFailOpenCache, FailOpenCache>();
        services.AddScoped<ISaldoCacheInvalidator, SaldoCacheInvalidator>();
        services.AddScoped<ISaldoDiarioReadRepository>(sp => new CachedSaldoDiarioReadRepository(
            sp.GetRequiredService<SaldoReadRepository>(),
            sp.GetRequiredService<IFailOpenCache>()));
        return services;
    }

    private static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var opcoes = configuration.GetSection(MessagingOptions.Section).Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<LancamentoCriadoLoteConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbit = configuration.GetSection("RabbitMQ");
                cfg.Host(rabbit["Host"], "/", h =>
                {
                    h.Username(rabbit["Username"]!);
                    h.Password(rabbit["Password"]!);
                });

                cfg.ReceiveEndpoint(
                    LancamentoEndpointConfigurator.Fila,
                    e => LancamentoEndpointConfigurator.Configurar(e, context, opcoes));
            });
        });

        return services;
    }
}
