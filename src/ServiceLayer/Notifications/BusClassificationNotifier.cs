#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.Contracts.Notifications;
using Fistix.TaskManager.ViewModel.Dtos;
using MassTransit;

namespace Fistix.TaskManager.ServiceLayer.Notifications;

/// <summary>Publishes typed notify events for the Notification service (Phase 4/5 choreography).</summary>
public sealed class BusClassificationNotifier : IClassificationNotifier
{
    private readonly IPublishEndpoint _publishEndpoint;

    public BusClassificationNotifier(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task NotifyAsync(TaskClassificationDto classification, CancellationToken cancellationToken = default)
    {
        return _publishEndpoint.Publish(
            new TodoClassificationUpdated(
                classification.TodoExternalId,
                classification.Status ?? string.Empty,
                classification.SuggestedPriority,
                classification.Confidence,
                classification.Reason,
                classification.GeneratedAt ?? DateTime.UtcNow),
            cancellationToken);
    }
}
