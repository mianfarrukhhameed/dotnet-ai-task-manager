#nullable enable

using System;
using Fistix.TaskManager.Contracts.Notifications;
using Fistix.TaskManager.Contracts.Sagas;
using Fistix.TaskManager.Core.DomainModel.Constants;
using Fistix.TaskManager.DataLayer.Sagas;
using Fistix.TaskManager.ServiceLayer.Todos;
using MassTransit;

namespace Fistix.TaskManager.AiJobsService.Sagas;

/// <summary>
/// Optional Phase 5 demo saga: approve → Todo creates sprint → finalize job → notify.
/// CorrelationId = JobExternalId.
/// </summary>
public sealed class SprintApproveStateMachine : MassTransitStateMachine<SprintApproveState>
{
    public const int CreateTimeoutSeconds = 90;

    public SprintApproveStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => ApproveRequested, e =>
        {
            e.CorrelateById(ctx => ctx.Message.JobExternalId);
            e.InsertOnInitial = true;
            e.SetSagaFactory(ctx => new SprintApproveState
            {
                CorrelationId = ctx.Message.JobExternalId,
                OwnerUserId = ctx.Message.OwnerUserId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        });

        Event(() => SprintCreated, e => e.CorrelateById(ctx => ctx.Message.JobExternalId));
        Event(() => SprintCreateFailed, e => e.CorrelateById(ctx => ctx.Message.JobExternalId));

        Schedule(() => CreateTimeout, instance => instance.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromSeconds(CreateTimeoutSeconds);
            s.Received = r => r.CorrelateById(ctx => ctx.Message.JobExternalId);
        });

        Initially(
            When(ApproveRequested)
                .Then(ctx =>
                {
                    ctx.Saga.OwnerUserId = ctx.Message.OwnerUserId;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .Publish(ctx => new CreateSprintFromProposal(
                    ctx.Message.JobExternalId,
                    ctx.Message.OwnerUserId,
                    ctx.Message.SelectedTaskExternalIds,
                    ctx.Message.Reasoning,
                    ctx.Message.StepsJson,
                    ctx.Message.SprintName,
                    ctx.Message.DurationDays,
                    ctx.Message.MaxTasks))
                .Schedule(CreateTimeout, ctx => new SprintApproveTimeoutExpired(ctx.Saga.CorrelationId))
                .TransitionTo(CreatingSprint));

        During(CreatingSprint,
            When(SprintCreated)
                .Unschedule(CreateTimeout)
                .ThenAsync(async ctx =>
                {
                    ctx.Saga.SprintExternalId = ctx.Message.SprintExternalId;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;

                    var finalize = ctx.GetServiceOrCreateInstance<SprintApproveFinalizeService>();
                    await finalize.CompleteAsync(
                        ctx.Message.JobExternalId,
                        ctx.Message.SprintExternalId,
                        ctx.Message.SprintName,
                        ctx.Message.StartDate,
                        ctx.Message.EndDate,
                        ctx.Message.SelectedTasksJson,
                        ctx.Message.Reasoning,
                        ctx.Message.StepsJson,
                        ctx.CancellationToken);
                })
                .Publish(ctx => new SprintApprovalNotify(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OwnerUserId,
                    ctx.Saga.SprintExternalId,
                    AiBatchJobStatus.Completed,
                    "Sprint created from approved proposal.",
                    DateTime.UtcNow))
                .TransitionTo(Completed)
                .Finalize(),

            When(SprintCreateFailed)
                .Unschedule(CreateTimeout)
                .ThenAsync(async ctx =>
                {
                    ctx.Saga.LastError = Truncate(ctx.Message.ErrorMessage, 2000);
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;

                    var finalize = ctx.GetServiceOrCreateInstance<SprintApproveFinalizeService>();
                    await finalize.FailAsync(
                        ctx.Message.JobExternalId,
                        ctx.Message.ErrorMessage,
                        ctx.CancellationToken);
                })
                .Publish(ctx => new SprintApprovalNotify(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OwnerUserId,
                    null,
                    AiBatchJobStatus.Failed,
                    ctx.Saga.LastError ?? "Sprint approval failed.",
                    DateTime.UtcNow))
                .TransitionTo(Failed)
                .Finalize(),

            When(CreateTimeout.Received)
                .ThenAsync(async ctx =>
                {
                    ctx.Saga.LastError = $"Timed out waiting for sprint create after {CreateTimeoutSeconds}s.";
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;

                    var finalize = ctx.GetServiceOrCreateInstance<SprintApproveFinalizeService>();
                    await finalize.FailAsync(
                        ctx.Saga.CorrelationId,
                        ctx.Saga.LastError,
                        ctx.CancellationToken);
                })
                .Publish(ctx => new SprintApprovalNotify(
                    ctx.Saga.CorrelationId,
                    ctx.Saga.OwnerUserId,
                    null,
                    AiBatchJobStatus.Failed,
                    ctx.Saga.LastError ?? "Sprint approval timed out.",
                    DateTime.UtcNow))
                .TransitionTo(Failed)
                .Finalize());

        SetCompletedWhenFinalized();
    }

    public State CreatingSprint { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<SprintApproveRequested> ApproveRequested { get; private set; } = null!;
    public Event<SprintCreated> SprintCreated { get; private set; } = null!;
    public Event<SprintCreateFailed> SprintCreateFailed { get; private set; } = null!;

    public Schedule<SprintApproveState, SprintApproveTimeoutExpired> CreateTimeout { get; private set; } = null!;

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
