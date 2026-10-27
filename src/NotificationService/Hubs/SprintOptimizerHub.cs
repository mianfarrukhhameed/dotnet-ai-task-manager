using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fistix.TaskManager.NotificationService.Hubs;

[Authorize]
public class SprintOptimizerHub : Hub
{
    public const string HubPath = "/hubs/sprint-optimizer";
    public const string JobUpdatedMethod = "SprintOptimizerUpdated";
    public const string ApprovalNotifyMethod = "SprintApprovalNotify";

    public Task JoinJob(Guid jobExternalId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(jobExternalId));

    public Task LeaveJob(Guid jobExternalId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(jobExternalId));

    public static string GetGroupName(Guid jobExternalId) => $"sprint-optimizer:{jobExternalId}";
}
