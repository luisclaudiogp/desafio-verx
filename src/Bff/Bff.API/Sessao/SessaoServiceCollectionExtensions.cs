using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using StackExchange.Redis;

namespace Bff.API.Sessao;

public static class SessaoServiceCollectionExtensions
{
    public static IServiceCollection AddSessaoBff(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var opcoes = configuration.GetSection(SessaoOptions.Secao).Get<SessaoOptions>() ?? new SessaoOptions();
        services.AddSingleton(opcoes);

        if (!opcoes.Habilitada)
            return services;

        return services
            .AddArmazenamentoDaSessao(opcoes)
            .AddAutenticacaoPorSessao(opcoes, configuration.GetSection("Keycloak"), environment);
    }

    private static IServiceCollection AddArmazenamentoDaSessao(this IServiceCollection services, SessaoOptions opcoes)
    {
        var redis = new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(opcoes.RedisConnectionString));

        services.AddSingleton(_ => redis.Value);
        services.AddStackExchangeRedisCache(o =>
        {
            o.ConnectionMultiplexerFactory = () => Task.FromResult(redis.Value);
            o.InstanceName = "bff:";
        });
        services.AddDataProtection()
            .SetApplicationName("verx-bff")
            .PersistKeysToStackExchangeRedis(() => redis.Value.GetDatabase(), "verx:bff:dp-keys");

        services.AddSingleton<ITicketStore>(provedor => new RedisTicketStore(
            provedor.GetRequiredService<IDistributedCache>(), provedor.GetRequiredService<TimeProvider>(), opcoes));
        services.AddScoped<RenovadorDeSessao>();
        services.AddOptions<CookieAuthenticationOptions>(SessaoSchemes.Cookie)
            .Configure<ITicketStore>((o, store) => o.SessionStore = store);

        return services;
    }

    private static IServiceCollection AddAutenticacaoPorSessao(
        this IServiceCollection services, SessaoOptions opcoes, IConfigurationSection keycloak, IHostEnvironment environment)
    {
        services.AddAuthentication(o =>
            {
                o.DefaultScheme = SessaoSchemes.Inteligente;
                o.DefaultChallengeScheme = SessaoSchemes.Inteligente;
                o.DefaultSignInScheme = SessaoSchemes.Cookie;
            })
            .AddPolicyScheme(SessaoSchemes.Inteligente, "Bearer ou sessao por cookie", o =>
                o.ForwardDefaultSelector = SelecionarEsquema)
            .AddCookie(SessaoSchemes.Cookie, o => ConfigurarCookie(o, opcoes, environment))
            .AddOpenIdConnect(SessaoSchemes.OpenId, o => ConfigurarOpenId(o, opcoes, keycloak, environment));

        return services;
    }

    private static string SelecionarEsquema(HttpContext context) =>
        context.Request.Headers.ContainsKey("Authorization")
            ? JwtBearerDefaults.AuthenticationScheme
            : SessaoSchemes.Cookie;

    private static CookieSecurePolicy PoliticaDeCookieSeguro(IHostEnvironment environment) =>
        environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

    private static void ConfigurarOpenId(
        OpenIdConnectOptions o, SessaoOptions opcoes, IConfigurationSection keycloak, IHostEnvironment environment)
    {
        o.SignInScheme = SessaoSchemes.Cookie;
        o.Authority = opcoes.Authority;
        o.RequireHttpsMetadata = keycloak.GetValue("RequireHttpsMetadata", true);
        o.ClientId = keycloak["ClientId"];
        o.ClientSecret = keycloak["ClientSecret"];
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.ResponseMode = OpenIdConnectResponseMode.Query;
        o.UsePkce = true;
        o.SaveTokens = true;
        o.GetClaimsFromUserInfoEndpoint = false;
        o.MapInboundClaims = false;
        o.CallbackPath = "/signin-oidc";
        o.SignedOutCallbackPath = "/signout-callback-oidc";
        o.Scope.Clear();
        o.Scope.Add(OpenIdConnectScope.OpenId);
        o.CorrelationCookie.SameSite = SameSiteMode.Lax;
        o.NonceCookie.SameSite = SameSiteMode.Lax;
        o.CorrelationCookie.SecurePolicy = PoliticaDeCookieSeguro(environment);
        o.NonceCookie.SecurePolicy = PoliticaDeCookieSeguro(environment);
        o.BackchannelHttpHandler = new ReescritaDoHostDoIdpHandler(opcoes.UrlPublicaDoIdp, opcoes.UrlInternaDoIdp)
        {
            InnerHandler = new HttpClientHandler()
        };
    }

    private static void ConfigurarCookie(CookieAuthenticationOptions o, SessaoOptions opcoes, IHostEnvironment environment)
    {
        o.Cookie.Name = opcoes.NomeDoCookie;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = PoliticaDeCookieSeguro(environment);
        o.ExpireTimeSpan = TimeSpan.FromMinutes(opcoes.InatividadeMinutos);
        o.SlidingExpiration = true;

        o.Events.OnRedirectToLogin = context => ResponderComStatus(context.Response, StatusCodes.Status401Unauthorized);
        o.Events.OnRedirectToAccessDenied = context => ResponderComStatus(context.Response, StatusCodes.Status403Forbidden);
        o.Events.OnValidatePrincipal = RenovarOuEncerrarAsync;
    }

    private static Task ResponderComStatus(HttpResponse resposta, int status)
    {
        resposta.StatusCode = status;
        return Task.CompletedTask;
    }

    private static async Task RenovarOuEncerrarAsync(CookieValidatePrincipalContext context)
    {
        var renovador = context.HttpContext.RequestServices.GetRequiredService<RenovadorDeSessao>();
        var resultado = await renovador.RenovarSeNecessarioAsync(context.Properties, context.HttpContext.RequestAborted);

        if (resultado == ResultadoRenovacao.Renovado)
        {
            context.ShouldRenew = true;
        }
        else if (resultado == ResultadoRenovacao.SessaoEncerrada)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(SessaoSchemes.Cookie);
        }
    }
}
