namespace Fistix.TaskManager.Contracts.Notifications;

/// <summary>Typed notify event — payload is serialized <c>AiBatchJobDto</c> JSON for SignalR fan-out.</summary>
public sealed record AiBatchJobUpdated(
    Guid JobExternalId,
    string Status,
    string PayloadJson,
    DateTime OccurredAtUtc);
