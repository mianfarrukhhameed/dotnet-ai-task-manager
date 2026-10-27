namespace Fistix.TaskManager.Contracts.Sagas;

/// <summary>Todo → saga: sprint create failed.</summary>
public sealed record SprintCreateFailed(
    Guid JobExternalId,
    string ErrorMessage,
    DateTime OccurredAtUtc);
