using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fistix.TaskManager.NotificationService.Hubs;

[Authorize]
public class KnowledgeIngestHub : Hub
{
    public const string HubPath = "/hubs/knowledge-ingest";
    public const string JobUpdatedMethod = "IngestJobUpdated";

    public Task JoinJob(Guid jobExternalId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(jobExternalId));

    public Task LeaveJob(Guid jobExternalId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(jobExternalId));

    public static string GetGroupName(Guid jobExternalId) => $"knowledge-ingest:{jobExternalId}";
}
