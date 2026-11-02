#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.Contracts.Notifications;
using Fistix.TaskManager.ViewModel.Dtos;
using MassTransit;

namespace Fistix.TaskManager.ServiceLayer.Notifications;

public sealed class BusAiBatchNotifier : IAiBatchNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPublishEndpoint _publishEndpoint;

    public BusAiBatchNotifier(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task NotifyAsync(AiBatchJobDto job, CancellationToken cancellationToken = default) =>
        _publishEndpoint.Publish(
            new AiBatchJobUpdated(
                job.ExternalId,
                job.Status,
                JsonSerializer.Serialize(job, JsonOptions),
                DateTime.UtcNow),
            cancellationToken);
}

public sealed class BusSprintOptimizerNotifier : ISprintOptimizerNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPublishEndpoint _publishEndpoint;

    public BusSprintOptimizerNotifier(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task NotifyAsync(SprintOptimizerJobDto job, CancellationToken cancellationToken = default) =>
        _publishEndpoint.Publish(
            new SprintOptimizerJobUpdated(
                job.ExternalId,
                job.Status,
                JsonSerializer.Serialize(job, JsonOptions),
                DateTime.UtcNow),
            cancellationToken);
}

public sealed class BusKnowledgeIngestNotifier : IKnowledgeIngestNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPublishEndpoint _publishEndpoint;

    public BusKnowledgeIngestNotifier(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task NotifyAsync(KnowledgeIngestJobDto job, CancellationToken cancellationToken = default) =>
        _publishEndpoint.Publish(
            new KnowledgeIngestUpdated(
                job.ExternalId,
                job.Status,
                JsonSerializer.Serialize(job, JsonOptions),
                DateTime.UtcNow),
            cancellationToken);
}
