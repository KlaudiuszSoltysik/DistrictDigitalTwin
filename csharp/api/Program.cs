using api;
using api.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddDbContext<TelemetryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TelemetryDbConnection")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SimulationTelemetryConsumer>();
    x.AddConsumer<SimulationStatusConsumer>();
    x.AddConsumer<DigitalTwinTelemetryConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("RabbitMqConnection");

        var uri = new Uri(connectionString ?? throw new InvalidOperationException("Invalid connection string."));

        cfg.Host(uri.Host, (ushort)uri.Port, uri.AbsolutePath, h =>
        {
            var parts = uri.UserInfo.Split(':');
            h.Username(parts[0]);
            h.Password(parts[1]);
        });

        cfg.UseRawJsonSerializer();

        cfg.ReceiveEndpoint("simulation-telemetry-queue", e =>
        {
            e.Bind("simulation-telemetry.exchange");
            e.ConfigureConsumer<SimulationTelemetryConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("digital-twin-telemetry-queue", e =>
        {
            e.Bind("digital-twin-telemetry.exchange");
            e.ConfigureConsumer<DigitalTwinTelemetryConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("simulation-status", e =>
        {
            e.Durable = false;
            e.SetQueueArgument("x-message-ttl", 1000);
            e.ConfigureConsumer<SimulationStatusConsumer>(ctx);
        });
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<CacheService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

using (var scope = app.Services.CreateScope())
{
    var cacheService = scope.ServiceProvider.GetRequiredService<CacheService>();
    await cacheService.InitializeCacheAsync();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.MapHub<TelemetryHub>("/hubs/simulation");
app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}