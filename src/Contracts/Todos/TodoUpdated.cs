namespace Fistix.TaskManager.Contracts.Todos;

/// <summary>
/// Published when a todo is updated. <see cref="TextChanged"/> gates classify/embed side effects (D29).
/// </summary>
public sealed record TodoUpdated(
    Guid TodoExternalId,
    Guid OwnerExternalId,
    bool TextChanged,
    DateTime OccurredAtUtc);
