namespace Fistix.TaskManager.Contracts.Notifications;

public sealed record AiBatchJobUpdated(
    Guid JobExternalId,
    string Status,
    string? Message,
    DateTime OccurredAtUtc);
