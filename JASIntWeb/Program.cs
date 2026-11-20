using JASIntWeb.Components;
using JASIntWeb.Endpoints;
using JASIntWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddStoreServices();

var app = builder.Build();
await app.InitializeDatabaseAsync();
app.UseStorePipeline();

app.MapStoreEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(JASIntWeb.Client._Imports).Assembly);
await app.RunAsync();
