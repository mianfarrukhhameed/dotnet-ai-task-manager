namespace Fistix.TaskManager.Contracts.Notifications;

/// <summary>Typed notify event (D68) — Notification service → SignalR classification hub.</summary>
public sealed record TodoClassificationUpdated(
    Guid TodoExternalId,
    string Status,
    string? SuggestedPriority,
    float? Confidence,
    string? Reason,
    DateTime OccurredAtUtc);
