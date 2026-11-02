using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fistix.TaskManager.NotificationService.Hubs;

/// <summary>
/// SignalR hub owned by Notification service (Phase 4). Join is JWT-gated;
/// ownership checks remain on Todo HTTP APIs.
/// </summary>
[Authorize]
public class ClassificationHub : Hub
{
    public const string HubPath = "/hubs/classification";
    public const string ClassificationUpdatedMethod = "ClassificationUpdated";

    public Task JoinTodo(Guid todoExternalId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(todoExternalId));

    public Task LeaveTodo(Guid todoExternalId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(todoExternalId));

    public static string GetGroupName(Guid todoExternalId) => $"todo-classification:{todoExternalId}";
}
