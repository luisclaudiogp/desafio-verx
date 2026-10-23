using System.Net;

namespace Bff.UnitTests;

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> resposta) : HttpMessageHandler
{
    public List<(Uri? Uri, string Corpo)> Requisicoes { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requisicoes.Add((request.RequestUri, corpo));
        return resposta(request);
    }

    public static StubHttpMessageHandler Retornando(HttpStatusCode status, string json = "{}") =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(json) });
}
