using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using website;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var baseApiUrl = builder.Configuration["BaseApiUrl"] ?? "/";

builder.Services.AddScoped<SimulationStatusService>(_ =>
{
    var hubConnection = new HubConnectionBuilder()
        .WithUrl($"{baseApiUrl}hubs/simulation")
        .WithAutomaticReconnect()
        .Build();

    var apiHttpClient = new HttpClient { BaseAddress = new Uri(baseApiUrl) };

    return new SimulationStatusService(hubConnection, apiHttpClient);
});

await builder.Build().RunAsync();