using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PortfolioCMS.Web;
using PortfolioCMS.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Point to the API (change port to match your API launch port)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000")
});

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<PortfolioApiService>();
builder.Services.AddScoped<PortfolioHubService>();

var host = builder.Build();

// Restore auth session before first render
var api = host.Services.GetRequiredService<PortfolioApiService>();
await api.InitializeAsync();

await host.RunAsync();
