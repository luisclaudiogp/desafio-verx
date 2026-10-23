namespace Bff.API.Sessao;

public sealed class ReescritaDoHostDoIdpHandler(string urlPublica, string urlInterna) : DelegatingHandler
{
    private readonly Uri _publica = new(urlPublica.TrimEnd('/') + "/");
    private readonly Uri _interna = new(urlInterna.TrimEnd('/') + "/");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestUri is { } uri && _publica.IsBaseOf(uri))
        {
            request.RequestUri = new UriBuilder(uri)
            {
                Scheme = _interna.Scheme,
                Host = _interna.Host,
                Port = _interna.Port
            }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
