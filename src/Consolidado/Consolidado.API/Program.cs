using Consolidado.Application;
using Consolidado.Infrastructure;
using Consolidado.Infrastructure.Data;
using Verx.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVerxApi(builder.Configuration, "Consolidado.API");
builder.Services.AddVerxHealthChecks(builder.Configuration)
    .AddRedis(builder.Configuration["Redis:ConnectionString"]!, name: "redis", tags: ["cache"]);
builder.Services.AddConsolidadoApplication();
builder.Services.AddConsolidadoInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseVerxApi();
await app.MigrateDatabaseAsync<ConsolidadoDbContext>();
app.MapControllers();

await app.RunAsync();
