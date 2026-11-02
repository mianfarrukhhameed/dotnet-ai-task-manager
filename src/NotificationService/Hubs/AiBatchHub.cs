using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fistix.TaskManager.NotificationService.Hubs;

[Authorize]
public class AiBatchHub : Hub
{
    public const string HubPath = "/hubs/ai-batch";
    public const string BatchUpdatedMethod = "BatchJobUpdated";

    public Task JoinJob(Guid jobExternalId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(jobExternalId));

    public Task LeaveJob(Guid jobExternalId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(jobExternalId));

    public static string GetGroupName(Guid jobExternalId) => $"ai-batch:{jobExternalId}";
}
