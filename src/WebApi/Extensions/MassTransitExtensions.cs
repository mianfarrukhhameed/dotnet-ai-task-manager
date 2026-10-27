#nullable enable

using System;
using Fistix.TaskManager.DataLayer;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fistix.TaskManager.WebApi.Extensions;

public static class MassTransitExtensions
{
    /// <summary>
    /// WebApi publishes via EF transactional outbox. Classify/embed consumers live in AiWorker (Phase 4).
    /// </summary>
    public static IServiceCollection AddTaskManagerMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<WriteEfContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.SetKebabCaseEndpointNameFormatter();

            x.UsingRabbitMq((context, cfg) =>
            {
                var connectionString = configuration.GetConnectionString("rabbitmq")
                    ?? configuration.GetConnectionString("RabbitMQ")
                    ?? "amqp://guest:guest@localhost:5672";

                cfg.Host(connectionString);
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
