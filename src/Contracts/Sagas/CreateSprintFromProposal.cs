namespace Fistix.TaskManager.Contracts.Sagas;

/// <summary>Saga → Todo: create a sprint from an approved optimizer proposal.</summary>
public sealed record CreateSprintFromProposal(
    Guid JobExternalId,
    Guid OwnerUserId,
    IReadOnlyList<Guid> SelectedTaskExternalIds,
    string Reasoning,
    string StepsJson,
    string? SprintName,
    int DurationDays,
    int MaxTasks);
