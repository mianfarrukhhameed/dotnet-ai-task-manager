#nullable enable

using System;
using MassTransit;

namespace Fistix.TaskManager.DataLayer.Sagas;

/// <summary>EF-backed MassTransit saga instance for sprint-approve (Phase 5 demo).</summary>
public sealed class SprintApproveState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }

    public Guid? SprintExternalId { get; set; }

    public string? LastError { get; set; }

    public Guid? TimeoutTokenId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
