#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Fistix.TaskManager.Contracts.Sagas;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Fistix.TaskManager.ServiceLayer.Todos;
using Fistix.TaskManager.ViewModel.Commands.Todos;
using Fistix.TaskManager.ViewModel.Dtos;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fistix.TaskManager.ServiceLayer.Messaging;

/// <summary>
/// Todo-side consumer for the sprint-approve saga: creates the sprint (idempotent via job.CreatedSprintId).
/// </summary>
public sealed class CreateSprintFromProposalConsumer : IConsumer<CreateSprintFromProposal>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISprintOptimizerJobRepository _jobRepository;
    private readonly SprintOptimizerPersistService _persistService;
    private readonly ILogger<CreateSprintFromProposalConsumer> _logger;

    public CreateSprintFromProposalConsumer(
        ISprintOptimizerJobRepository jobRepository,
        SprintOptimizerPersistService persistService,
        ILogger<CreateSprintFromProposalConsumer> logger)
    {
        _jobRepository = jobRepository;
        _persistService = persistService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateSprintFromProposal> context)
    {
        var message = context.Message;
        try
        {
            var job = await _jobRepository.GetByExternalIdAsync(message.JobExternalId, context.CancellationToken);
            if (job is null)
            {
                await context.Publish(new SprintCreateFailed(
                    message.JobExternalId,
                    $"Sprint optimizer job {message.JobExternalId} was not found.",
                    DateTime.UtcNow));
                return;
            }

            if (job.CreatedSprintId.HasValue)
            {
                var existing = BuildExistingResponse(job, message);
                await context.Publish(ToCreatedEvent(message, existing));
                return;
            }

            var selectedTodos = await _persistService.ResolveSelectedTodosAsync(
                message.OwnerUserId,
                message.SelectedTaskExternalIds,
                message.MaxTasks,
                context.CancellationToken);

            if (selectedTodos.Count == 0)
            {
                await context.Publish(new SprintCreateFailed(
                    message.JobExternalId,
                    "No valid tasks selected for sprint approval.",
                    DateTime.UtcNow));
                return;
            }

            // Apply name/duration from the approve payload for this create (shared MainDb interim).
            if (!string.IsNullOrWhiteSpace(message.SprintName))
            {
                job.Name = message.SprintName;
            }

            if (message.DurationDays > 0)
            {
                job.DurationDays = message.DurationDays;
            }

            var steps = DeserializeSteps(message.StepsJson);
            var response = await _persistService.PersistApprovedAsync(
                job,
                selectedTodos,
                message.Reasoning,
                steps,
                context.CancellationToken);

            // Persist CreatedSprintId for idempotency; job status remains owned by AiJobs finalize.
            await _jobRepository.UpdateAsync(job, context.CancellationToken);

            await context.Publish(ToCreatedEvent(message, response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSprintFromProposal failed for job {JobExternalId}", message.JobExternalId);
            await context.Publish(new SprintCreateFailed(
                message.JobExternalId,
                ex.Message,
                DateTime.UtcNow));
        }
    }

    private static OptimizeSprintResponseDto BuildExistingResponse(
        SprintOptimizerJob job,
        CreateSprintFromProposal message)
    {
        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(job.DurationDays > 0 ? job.DurationDays : message.DurationDays);
        var name = string.IsNullOrWhiteSpace(job.Name)
            ? (string.IsNullOrWhiteSpace(message.SprintName)
                ? $"Optimized Sprint {startDate:yyyy-MM-dd}"
                : message.SprintName!)
            : job.Name;

        return new OptimizeSprintResponseDto
        {
            SprintId = job.CreatedSprintId!.Value,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            Reasoning = message.Reasoning,
            Steps = DeserializeSteps(message.StepsJson),
            SelectedTasks = []
        };
    }

    private static SprintCreated ToCreatedEvent(CreateSprintFromProposal message, OptimizeSprintResponseDto response) =>
        new(
            message.JobExternalId,
            response.SprintId,
            response.Name,
            response.StartDate,
            response.EndDate,
            JsonSerializer.Serialize(response.SelectedTasks, JsonOptions),
            response.Reasoning,
            JsonSerializer.Serialize(response.Steps, JsonOptions),
            DateTime.UtcNow);

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
}
