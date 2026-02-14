using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using shared;
using telemetry_service;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<TelemetryDbContext>(options =>
            options.UseNpgsql(context.Configuration.GetConnectionString("HistoryDbConnection")));

        services.AddMassTransit(x =>
        {
            x.AddConsumer<SimulationTelemetryConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var connectionString = context.Configuration.GetConnectionString("RabbitMqConnection");

                var uri = new Uri(connectionString ??
                                  throw new InvalidOperationException("Invalid connection string."));

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
                    e.Bind("digital_twin_telemetry.exchange");
                    e.ConfigureConsumer<DigitalTwinTelemetryConsumer>(ctx);
                });
            });
        });

        services.AddHostedService<DbMigrationWorker>();
    })
    .Build();

await host.RunAsync();