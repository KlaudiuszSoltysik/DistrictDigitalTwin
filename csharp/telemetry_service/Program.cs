using MassTransit;
using Microsoft.EntityFrameworkCore;
using shared;
using telemetry_service;
using telemetry_service.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddDbContext<TelemetryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TelemetryDbConnection")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SimulationTelemetryConsumer>();
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

        cfg.ReceiveEndpoint("simulation-telemetry-queue-db", e =>
        {
            e.Bind("simulation-telemetry.exchange");
            e.ConfigureConsumer<SimulationTelemetryConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("digital-twin-telemetry-queue-db", e =>
        {
            e.Bind("digital-twin-telemetry.exchange");
            e.ConfigureConsumer<DigitalTwinTelemetryConsumer>(ctx);
        });
    });
});

builder.Services.AddHostedService<DbMigrationWorker>();

var app = builder.Build();

app.MapHealthChecks("/health");

await app.RunAsync();