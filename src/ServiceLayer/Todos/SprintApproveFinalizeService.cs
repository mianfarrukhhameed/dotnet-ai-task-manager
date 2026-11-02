#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.DomainModel.Constants;
using Fistix.TaskManager.ServiceLayer.Notifications;
using Fistix.TaskManager.ViewModel.Commands.Todos;
using Fistix.TaskManager.ViewModel.Dtos;

namespace Fistix.TaskManager.ServiceLayer.Todos;

/// <summary>Completes or fails a sprint-optimizer job after the approve saga finishes.</summary>
public sealed class SprintApproveFinalizeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISprintOptimizerJobRepository _jobRepository;
    private readonly ISprintOptimizerNotifier _notifier;

    public SprintApproveFinalizeService(
        ISprintOptimizerJobRepository jobRepository,
        ISprintOptimizerNotifier notifier)
    {
        _jobRepository = jobRepository;
        _notifier = notifier;
    }

    public async Task CompleteAsync(
        Guid jobExternalId,
        Guid sprintExternalId,
        string sprintName,
        DateTime startDate,
        DateTime endDate,
        string selectedTasksJson,
        string reasoning,
        string stepsJson,
        CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByExternalIdAsync(jobExternalId, cancellationToken);
        if (job is null)
        {
            return;
        }

        if (string.Equals(job.Status, AiBatchJobStatus.Completed, StringComparison.OrdinalIgnoreCase)
            && job.CreatedSprintId.HasValue)
        {
            await _notifier.NotifyAsync(SprintOptimizerJobMapper.ToDto(job), cancellationToken);
            return;
        }

        var selectedTasks = DeserializeSelectedTasks(selectedTasksJson);
        var steps = DeserializeSteps(stepsJson);

        var response = new OptimizeSprintResponseDto
        {
            SprintId = sprintExternalId,
            Name = sprintName,
            StartDate = startDate,
            EndDate = endDate,
            Reasoning = reasoning,
            Steps = steps,
            SelectedTasks = selectedTasks
        };

        job.CreatedSprintId = sprintExternalId;
        job.ResultJson = SprintOptimizerJobMapper.SerializeResult(response);
        job.ProposalJson = null;
        job.CheckpointJson = null;
        job.PendingRequestId = null;
        job.Status = AiBatchJobStatus.Completed;
        job.CurrentPhase = SprintOptimizerPhase.Done;
        job.StatusMessage = $"Sprint created with {selectedTasks.Count} tasks.";
        job.CompletedAt = DateTime.UtcNow;
        job.LastError = null;
        await _jobRepository.UpdateAsync(job, cancellationToken);
        await _notifier.NotifyAsync(SprintOptimizerJobMapper.ToDto(job), cancellationToken);
    }

    public async Task FailAsync(Guid jobExternalId, string errorMessage, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByExternalIdAsync(jobExternalId, cancellationToken);
        if (job is null)
        {
            return;
        }

        if (string.Equals(job.Status, AiBatchJobStatus.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(job.Status, AiBatchJobStatus.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(job.Status, AiBatchJobStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await _notifier.NotifyAsync(SprintOptimizerJobMapper.ToDto(job), cancellationToken);
            return;
        }

        job.Status = AiBatchJobStatus.Failed;
        job.CurrentPhase = SprintOptimizerPhase.Done;
        job.StatusMessage = "Sprint approval failed.";
        job.LastError = Truncate(errorMessage, 2000);
        job.CompletedAt = DateTime.UtcNow;
        await _jobRepository.UpdateAsync(job, cancellationToken);
        await _notifier.NotifyAsync(SprintOptimizerJobMapper.ToDto(job), cancellationToken);
    }

    private static List<SprintTaskSummaryDto> DeserializeSelectedTasks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<SprintTaskSummaryDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<AgentStepDto> DeserializeSteps(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AgentStepDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
