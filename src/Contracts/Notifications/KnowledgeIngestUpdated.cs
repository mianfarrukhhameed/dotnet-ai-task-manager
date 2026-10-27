namespace Fistix.TaskManager.Contracts.Notifications;

public sealed record KnowledgeIngestUpdated(
    Guid JobExternalId,
    string Status,
    string? Message,
    DateTime OccurredAtUtc);
