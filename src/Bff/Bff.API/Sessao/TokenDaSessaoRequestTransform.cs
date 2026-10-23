using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

namespace Bff.API.Sessao;

public sealed class TokenDaSessaoRequestTransform : RequestTransform
{
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.HttpContext.Request.Headers.ContainsKey(HeaderNames.Authorization))
            return;

        var token = await context.HttpContext.GetTokenAsync(SessaoSchemes.Cookie, RenovadorDeSessao.AccessToken);
        if (string.IsNullOrEmpty(token))
            return;

        context.ProxyRequest.Headers.Remove(HeaderNames.Authorization);
        context.ProxyRequest.Headers.TryAddWithoutValidation(HeaderNames.Authorization, "Bearer " + token);
    }
}
