using Fistix.TaskManager.ServiceLayer.Notifications;
using Fistix.TaskManager.WebApi.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fistix.TaskManager.WebApi.Extensions;

public static class SignalRServiceExtension
{
    public static IServiceCollection AddClassificationSignalR(this IServiceCollection services)
    {
        services.AddSignalR();
        // Classification UI updates go Notification service via bus (Phase 4/5).
        services.RemoveAll<IClassificationNotifier>();
        services.AddSingleton<IClassificationNotifier, BusClassificationNotifier>();
        services.AddSingleton<IAiBatchNotifier, SignalRAiBatchNotifier>();
        services.AddSingleton<ISprintOptimizerNotifier, SignalRSprintOptimizerNotifier>();
        services.AddSingleton<IKnowledgeIngestNotifier, SignalRKnowledgeIngestNotifier>();
        return services;
    }
}
