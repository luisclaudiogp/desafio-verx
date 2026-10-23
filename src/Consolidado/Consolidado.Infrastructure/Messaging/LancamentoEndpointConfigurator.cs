using MassTransit;
using Verx.SharedKernel;

namespace Consolidado.Infrastructure.Messaging;

public static class LancamentoEndpointConfigurator
{
    public const string Fila = "consolidado.lancamento-criado";

    public static void Configurar(
        IReceiveEndpointConfigurator endpoint, IRegistrationContext context, MessagingOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(opcoes);

        if (endpoint is IRabbitMqReceiveEndpointConfigurator rabbit)
            rabbit.PrefetchCount = opcoes.PrefetchCount;
        endpoint.ConcurrentMessageLimit = opcoes.ConcorrenciaMaxima;

        endpoint.UseMessageRetry(r =>
        {
            r.Exponential(
                opcoes.RetryTentativas,
                TimeSpan.FromMilliseconds(opcoes.RetryMinimoMs),
                TimeSpan.FromSeconds(opcoes.RetryMaximoSegundos),
                TimeSpan.FromSeconds(opcoes.RetryDeltaSegundos));
            r.Ignore<DomainException>();
        });

        endpoint.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromSeconds(opcoes.CircuitBreakerJanelaSegundos);
            cb.TripThreshold = opcoes.CircuitBreakerFalhasParaAbrir;
            cb.ActiveThreshold = opcoes.CircuitBreakerMinimoAtivo;
            cb.ResetInterval = TimeSpan.FromSeconds(opcoes.CircuitBreakerResetSegundos);
        });

        endpoint.ConfigureConsumer<LancamentoCriadoLoteConsumer>(context, consumer =>
            consumer.Options<BatchOptions>(batch => batch
                .SetMessageLimit(opcoes.LoteTamanho)
                .SetTimeLimit(TimeSpan.FromMilliseconds(opcoes.LoteJanelaMs))
                .SetConcurrencyLimit(opcoes.LoteConcorrencia)));
    }
}
