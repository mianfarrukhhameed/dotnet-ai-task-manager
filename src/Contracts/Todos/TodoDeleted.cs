namespace Fistix.TaskManager.Contracts.Todos;

/// <summary>Published when a todo is deleted. Consumers re-read / no-op if already gone.</summary>
public sealed record TodoDeleted(
    Guid TodoExternalId,
    Guid OwnerExternalId,
    DateTime OccurredAtUtc);
