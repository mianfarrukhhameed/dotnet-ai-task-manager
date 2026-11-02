namespace Fistix.TaskManager.Contracts.Todos;

/// <summary>Published when a todo is created. Consumers re-read current state from DB (idempotent).</summary>
public sealed record TodoCreated(
    Guid TodoExternalId,
    Guid OwnerExternalId,
    DateTime OccurredAtUtc);
