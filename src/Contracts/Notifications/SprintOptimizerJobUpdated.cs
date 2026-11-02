namespace Fistix.TaskManager.Contracts.Notifications;

public sealed record SprintOptimizerJobUpdated(
    Guid JobExternalId,
    string Status,
    string PayloadJson,
    DateTime OccurredAtUtc);
