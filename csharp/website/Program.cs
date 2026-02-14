using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using website;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var baseApiUrl = builder.Configuration["BaseApiUrl"] ?? throw new InvalidOperationException();

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(baseApiUrl)
});

builder.Services.AddScoped<SimulationStatusService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();

    var hubConnection = new HubConnectionBuilder()
        .WithUrl($"{baseApiUrl}/hubs/simulation")
        .WithAutomaticReconnect()
        .Build();

    return new SimulationStatusService(hubConnection, httpClient);
});

await builder.Build().RunAsync();