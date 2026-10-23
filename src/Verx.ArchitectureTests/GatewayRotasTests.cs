using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Verx.ArchitectureTests;

public partial class GatewayRotasTests
{
    [GeneratedRegex(@"\{(?<nome>\w+)(:[^}]*)?\}")]
    private static partial Regex Parametro();

    private static readonly Assembly[] ApisInternas =
    [
        typeof(Lancamentos.API.Controllers.LancamentosController).Assembly,
        typeof(Consolidado.API.Controllers.SaldosController).Assembly
    ];

    private static JsonElement ConfiguracaoDoBff()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Verx.Finance.slnx")))
            dir = dir.Parent;

        var arquivo = Path.Combine(dir!.FullName, "src", "Bff", "Bff.API", "appsettings.json");
        return JsonDocument.Parse(File.ReadAllText(arquivo)).RootElement.GetProperty("ReverseProxy");
    }

    private static IEnumerable<(string Nome, string Padrao, JsonElement Rota)> RotasDoGateway() =>
        ConfiguracaoDoBff().GetProperty("Routes").EnumerateObject()
            .Select(r => (r.Name, r.Value.GetProperty("Match").GetProperty("Path").GetString()!, r.Value));

    private static string Exemplo(string template) =>
        Parametro().Replace(template, m => m.Groups["nome"].Value switch
        {
            "data" => "2026-10-19",
            var n when n.EndsWith("Id", StringComparison.OrdinalIgnoreCase) => Guid.NewGuid().ToString(),
            _ => "x"
        });

    private static IEnumerable<string> EndpointsDosControllers() =>
        ApisInternas.SelectMany(a => a.GetTypes())
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(controller =>
            {
                var raiz = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
                return controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes().OfType<IRouteTemplateProvider>())
                    .Select(a => "/" + raiz.Trim('/') + (string.IsNullOrEmpty(a.Template) ? string.Empty : "/" + a.Template.Trim('/')));
            })
            .Select(Exemplo);

    private static bool Casa(string padrao, string caminho) =>
        new TemplateMatcher(TemplateParser.Parse(padrao), new RouteValueDictionary())
            .TryMatch(new PathString(caminho), new RouteValueDictionary());

    [Fact]
    public void TodoEndpointDasApisInternasTemRotaNoGateway()
    {
        var padroes = RotasDoGateway().Select(r => r.Padrao).ToList();

        var descobertos = EndpointsDosControllers().Where(c => !padroes.Any(p => Casa(p, c))).ToList();

        Assert.True(descobertos.Count == 0, "Endpoints sem rota no BFF: " + string.Join(", ", descobertos));
    }

    [Fact]
    public void TodaRotaDoGatewayCorrespondeAUmEndpointReal()
    {
        var endpoints = EndpointsDosControllers().ToList();

        var orfas = RotasDoGateway().Where(r => !endpoints.Any(e => Casa(r.Padrao, e))).Select(r => r.Nome).ToList();

        Assert.True(orfas.Count == 0, "Rotas do BFF sem endpoint: " + string.Join(", ", orfas));
    }

    [Fact]
    public void TodaRotaDoGatewayExigeAutorizacaoELimiteDeTaxa()
    {
        var fracas = RotasDoGateway()
            .Where(r => !r.Rota.TryGetProperty("AuthorizationPolicy", out _) || !r.Rota.TryGetProperty("RateLimiterPolicy", out _))
            .Select(r => r.Nome).ToList();

        Assert.True(fracas.Count == 0, "Rotas sem politica de autorizacao/rate limit: " + string.Join(", ", fracas));
    }

    [Fact]
    public void TodaRotaApontaParaUmClusterExistente()
    {
        var clusters = ConfiguracaoDoBff().GetProperty("Clusters").EnumerateObject().Select(c => c.Name).ToHashSet();

        var invalidas = RotasDoGateway()
            .Where(r => !clusters.Contains(r.Rota.GetProperty("ClusterId").GetString()!))
            .Select(r => r.Nome).ToList();

        Assert.True(invalidas.Count == 0, "Rotas com cluster inexistente: " + string.Join(", ", invalidas));
    }
}
