using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using shared;
using telemetry_service.Consumers;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Fatal()
    .Enrich.WithProperty("service", "telemetry service")
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseSerilog();

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

    var app = builder.Build();

    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Telemetry Service terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}