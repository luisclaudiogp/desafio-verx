using Lancamentos.Application;
using Lancamentos.Infrastructure;
using Lancamentos.Infrastructure.Data;
using Verx.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVerxApi(builder.Configuration, "Lancamentos.API");
builder.Services.AddVerxHealthChecks(builder.Configuration);
builder.Services.AddLancamentosApplication();
builder.Services.AddLancamentosInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseVerxApi();
await app.MigrateDatabaseAsync<LancamentosDbContext>();
app.MapControllers();

await app.RunAsync();
