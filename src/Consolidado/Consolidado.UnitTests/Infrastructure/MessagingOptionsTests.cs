using Consolidado.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace Consolidado.UnitTests.Infrastructure;

public class MessagingOptionsTests
{
    [Fact]
    public void Padroes_SaoConservadoresEConsistentes()
    {
        var o = new MessagingOptions();

        Assert.Equal(500, o.LoteTamanho);
        Assert.Equal(200, o.LoteJanelaMs);
        Assert.Equal(4, o.LoteConcorrencia);
        Assert.True(o.PrefetchCount >= o.LoteTamanho * o.LoteConcorrencia,
            "O prefetch deve comportar os lotes em andamento.");
        Assert.Equal(6, o.RetryTentativas);
    }

    [Fact]
    public void Configuracao_SobrescrevePadroes()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:LoteTamanho"] = "100",
                ["Messaging:RetryTentativas"] = "3"
            })
            .Build();

        var o = config.GetSection(MessagingOptions.Section).Get<MessagingOptions>()!;

        Assert.Equal(100, o.LoteTamanho);
        Assert.Equal(3, o.RetryTentativas);
        Assert.Equal(4, o.LoteConcorrencia);
    }
}
