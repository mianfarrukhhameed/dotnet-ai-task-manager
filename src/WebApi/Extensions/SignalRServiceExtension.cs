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
        services.AddSingleton<IClassificationNotifier, BusClassificationNotifier>();
        services.AddSingleton<IAiBatchNotifier, BusAiBatchNotifier>();
        services.AddSingleton<ISprintOptimizerNotifier, BusSprintOptimizerNotifier>();
        services.AddSingleton<IKnowledgeIngestNotifier, BusKnowledgeIngestNotifier>();
        return services;
    }
}
