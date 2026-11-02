namespace Fistix.TaskManager.Contracts.Sagas;

/// <summary>Scheduled timeout while waiting for Todo to create the sprint.</summary>
public sealed record SprintApproveTimeoutExpired(Guid JobExternalId);
