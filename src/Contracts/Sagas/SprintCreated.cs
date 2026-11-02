namespace Fistix.TaskManager.Contracts.Sagas;

/// <summary>Todo → saga: sprint persisted successfully.</summary>
public sealed record SprintCreated(
    Guid JobExternalId,
    Guid SprintExternalId,
    string SprintName,
    DateTime StartDate,
    DateTime EndDate,
    string SelectedTasksJson,
    string Reasoning,
    string StepsJson,
    DateTime OccurredAtUtc);
