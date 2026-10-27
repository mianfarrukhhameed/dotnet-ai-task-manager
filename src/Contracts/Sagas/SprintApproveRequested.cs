namespace Fistix.TaskManager.Contracts.Sagas;

/// <summary>Published by AiJobs after HTTP approve; starts the SprintApprove saga.</summary>
public sealed record SprintApproveRequested(
    Guid JobExternalId,
    Guid OwnerUserId,
    IReadOnlyList<Guid> SelectedTaskExternalIds,
    string Reasoning,
    string StepsJson,
    string? SprintName,
    int DurationDays,
    int MaxTasks,
    DateTime OccurredAtUtc);
