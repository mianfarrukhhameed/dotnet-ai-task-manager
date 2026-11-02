using Fistix.TaskManager.ServiceLayer.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fistix.TaskManager.WebApi.Extensions;

public static class SignalRServiceExtension
{
    public static IServiceCollection AddClassificationSignalR(this IServiceCollection services)
    {
        // VoiceTranscribe hub still hosted here; progress hubs moved to NotificationService.
        services.AddSignalR();
        services.RemoveAll<IClassificationNotifier>();
        services.RemoveAll<IAiBatchNotifier>();
        services.RemoveAll<ISprintOptimizerNotifier>();
        services.RemoveAll<IKnowledgeIngestNotifier>();
        // Scoped: MassTransit IPublishEndpoint is scoped.
        services.AddScoped<IClassificationNotifier, BusClassificationNotifier>();
        services.AddScoped<IAiBatchNotifier, BusAiBatchNotifier>();
        services.AddScoped<ISprintOptimizerNotifier, BusSprintOptimizerNotifier>();
        services.AddScoped<IKnowledgeIngestNotifier, BusKnowledgeIngestNotifier>();
        return services;
    }
}
