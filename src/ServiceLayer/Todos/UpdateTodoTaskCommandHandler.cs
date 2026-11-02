using AutoMapper;
using Fistix.TaskManager.AiLayer.Observability;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.Contracts.Todos;
using Fistix.TaskManager.Core;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.SecurityModel;
using Fistix.TaskManager.DataLayer;
using Fistix.TaskManager.ViewModel.Commands.Todos;
using Fistix.TaskManager.ViewModel.Dtos;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fistix.TaskManager.ServiceLayer.Todos;

public class UpdateTodoTaskCommandHandler : IRequestHandler<UpdateTodoTaskCommand, UpdateTodoTaskCommandResult>
{
    private readonly IMapper _mapper;
    private readonly ITodoTaskRepository _todoTaskRepository;
    private readonly WriteEfContext _write;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly AiConfiguration _aiConfig;
    private readonly IAiTelemetry _telemetry;
    private readonly ILogger<UpdateTodoTaskCommandHandler> _logger;

    public UpdateTodoTaskCommandHandler(
        IMapper mapper,
        ITodoTaskRepository todoTaskRepository,
        WriteEfContext write,
        ICurrentUserService currentUserService,
        IPublishEndpoint publishEndpoint,
        AiConfiguration aiConfig,
        ILogger<UpdateTodoTaskCommandHandler> logger,
        IAiTelemetry? telemetry = null)
    {
        _mapper = mapper;
        _todoTaskRepository = todoTaskRepository;
        _write = write;
        _currentUserService = currentUserService;
        _publishEndpoint = publishEndpoint;
        _aiConfig = aiConfig;
        _logger = logger;
        _telemetry = telemetry ?? NullAiTelemetry.Instance;
    }

    public async Task<UpdateTodoTaskCommandResult> Handle(UpdateTodoTaskCommand command, CancellationToken cancellationToken)
    {
        var todoTask = await _todoTaskRepository.Get(command.ExternalId, cancellationToken);

        TodoAccessGuard.EnsureCanAccess(todoTask, _currentUserService);

        var textChanged =
            !string.Equals(todoTask.Title, command.Title, StringComparison.Ordinal)
            || !string.Equals(todoTask.Description ?? string.Empty, command.Description ?? string.Empty, StringComparison.Ordinal);

        todoTask.Title = command.Title;
        todoTask.Description = command.Description;
        todoTask.DueDate = DateTimeUtc.EnsureUtc(command.DueDate);
        todoTask.Priority = ClassificationGuardrails.ToTaskPriority(command.Priority);

        var metadata = todoTask.AiMetadata;
        if (metadata is not null && !string.IsNullOrWhiteSpace(metadata.AiPriority))
        {
            var wasOverridden = ClassificationGuardrails.IsPriorityOverridden(
                todoTask.Priority,
                metadata.AiPriority);
            metadata.WasOverridden = wasOverridden;
            metadata.UpdatedAt = DateTime.UtcNow;
            var band = AiTelemetryNames.ConfidenceBands.FromConfidence(metadata.ConfidenceScore ?? 0f);
            _telemetry.RecordOverrideDecision(band, wasOverridden);
        }

        await _publishEndpoint.Publish(
            new TodoUpdated(
                todoTask.ExternalId,
                todoTask.CreatedByUserId,
                textChanged,
                DateTime.UtcNow),
            cancellationToken);

        await _write.SaveChangesAsync(cancellationToken);

        if (textChanged && (_aiConfig.Features.EnableEmbeddings || _aiConfig.Features.EnableClassification))
        {
            _logger.LogInformation(
                "Published TodoUpdated (text changed) for todo {TodoExternalId}",
                todoTask.ExternalId);
        }

        // Map from in-memory tracked entity — no post-mutate re-query (Phase 1 D4).
        return new UpdateTodoTaskCommandResult
        {
            Payload = _mapper.Map<TodoTaskDto>(todoTask)
        };
    }
}
