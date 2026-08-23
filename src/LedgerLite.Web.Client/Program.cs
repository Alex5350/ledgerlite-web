using LedgerLite.Web.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddLedgerLiteClientServices(builder.Configuration);
// .NET 10: the AddAuthorization() sugar moved to the Microsoft.Extensions.Authorization
// package; AddAuthorizationCore() registers the same component authorization services.
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
