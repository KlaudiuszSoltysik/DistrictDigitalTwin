using Microsoft.Extensions.Hosting;
using history_service;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<HistoryDbContext>(options =>
            options.UseNpgsql(context.Configuration.GetConnectionString("HistoryDbConnection")));

        services.AddMassTransit(x =>
        {
            x.AddConsumer<TelemetryConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var connectionString = context.Configuration.GetConnectionString("RabbitMqConnection");

                var uri = new Uri(connectionString ?? throw new InvalidOperationException("Invalid connection string."));

                cfg.Host(uri.Host, (ushort)uri.Port, uri.AbsolutePath, h =>
                {
                    var parts = uri.UserInfo.Split(':');
                    h.Username(parts[0]);
                    h.Password(parts[1]);
                });

                cfg.ReceiveEndpoint("history-service-queue", e =>
                {
                    e.Bind("district.telemetry.exchange");
                    e.UseRawJsonSerializer();
                    e.ConfigureConsumer<TelemetryConsumer>(ctx);
                });
            });
        });

        services.AddHostedService<DbMigrationWorker>();
    })
    .Build();

await host.RunAsync();