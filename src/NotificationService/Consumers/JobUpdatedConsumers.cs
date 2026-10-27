using System.Text.Json;
using Fistix.TaskManager.Contracts.Notifications;
using Fistix.TaskManager.NotificationService.Hubs;
using Fistix.TaskManager.ViewModel.Dtos;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Fistix.TaskManager.NotificationService.Consumers;

public sealed class AiBatchJobUpdatedConsumer : IConsumer<AiBatchJobUpdated>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHubContext<AiBatchHub> _hubContext;

    public AiBatchJobUpdatedConsumer(IHubContext<AiBatchHub> hubContext) => _hubContext = hubContext;

    public Task Consume(ConsumeContext<AiBatchJobUpdated> context)
    {
        var dto = JsonSerializer.Deserialize<AiBatchJobDto>(context.Message.PayloadJson, JsonOptions)
                  ?? new AiBatchJobDto { ExternalId = context.Message.JobExternalId, Status = context.Message.Status };
        return _hubContext.Clients
            .Group(AiBatchHub.GetGroupName(context.Message.JobExternalId))
            .SendAsync(AiBatchHub.BatchUpdatedMethod, dto, context.CancellationToken);
    }
}

public sealed class SprintOptimizerJobUpdatedConsumer : IConsumer<SprintOptimizerJobUpdated>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHubContext<SprintOptimizerHub> _hubContext;

    public SprintOptimizerJobUpdatedConsumer(IHubContext<SprintOptimizerHub> hubContext) => _hubContext = hubContext;

    public Task Consume(ConsumeContext<SprintOptimizerJobUpdated> context)
    {
        var dto = JsonSerializer.Deserialize<SprintOptimizerJobDto>(context.Message.PayloadJson, JsonOptions)
                  ?? new SprintOptimizerJobDto { ExternalId = context.Message.JobExternalId, Status = context.Message.Status };
        return _hubContext.Clients
            .Group(SprintOptimizerHub.GetGroupName(context.Message.JobExternalId))
            .SendAsync(SprintOptimizerHub.JobUpdatedMethod, dto, context.CancellationToken);
    }
}

public sealed class KnowledgeIngestUpdatedConsumer : IConsumer<KnowledgeIngestUpdated>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHubContext<KnowledgeIngestHub> _hubContext;

    public KnowledgeIngestUpdatedConsumer(IHubContext<KnowledgeIngestHub> hubContext) => _hubContext = hubContext;

    public Task Consume(ConsumeContext<KnowledgeIngestUpdated> context)
    {
        var dto = JsonSerializer.Deserialize<KnowledgeIngestJobDto>(context.Message.PayloadJson, JsonOptions)
                  ?? new KnowledgeIngestJobDto { ExternalId = context.Message.JobExternalId, Status = context.Message.Status };
        return _hubContext.Clients
            .Group(KnowledgeIngestHub.GetGroupName(context.Message.JobExternalId))
            .SendAsync(KnowledgeIngestHub.JobUpdatedMethod, dto, context.CancellationToken);
    }
}
