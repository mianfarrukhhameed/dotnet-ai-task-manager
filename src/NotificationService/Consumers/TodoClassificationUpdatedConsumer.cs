using Fistix.TaskManager.Contracts.Notifications;
using Fistix.TaskManager.NotificationService.Hubs;
using Fistix.TaskManager.ViewModel.Dtos;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Fistix.TaskManager.NotificationService.Consumers;

public sealed class TodoClassificationUpdatedConsumer : IConsumer<TodoClassificationUpdated>
{
    private readonly IHubContext<ClassificationHub> _hubContext;

    public TodoClassificationUpdatedConsumer(IHubContext<ClassificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task Consume(ConsumeContext<TodoClassificationUpdated> context)
    {
        var message = context.Message;
        var dto = new TaskClassificationDto
        {
            TodoExternalId = message.TodoExternalId,
            Status = message.Status,
            SuggestedPriority = message.SuggestedPriority,
            Confidence = message.Confidence,
            Reason = message.Reason,
            GeneratedAt = message.OccurredAtUtc
        };

        return _hubContext.Clients
            .Group(ClassificationHub.GetGroupName(message.TodoExternalId))
            .SendAsync(
                ClassificationHub.ClassificationUpdatedMethod,
                dto,
                context.CancellationToken);
    }
}
