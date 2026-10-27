namespace Fistix.TaskManager.Contracts.Notifications;

/// <summary>Optional toast / approve-complete push (D68).</summary>
public sealed record SprintApprovalNotify(
    Guid JobExternalId,
    Guid OwnerUserId,
    Guid? SprintExternalId,
    string Status,
    string Message,
    DateTime OccurredAtUtc);
