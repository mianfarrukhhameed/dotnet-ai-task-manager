using AutoMapper;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.Contracts.Todos;
using Fistix.TaskManager.Core;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Fistix.TaskManager.Core.DomainModel.Constants;
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

namespace Fistix.TaskManager.ServiceLayer.Todos
{
  public class CreateTodoTaskCommandHandler : IRequestHandler<CreateTodoTaskCommand, CreateTodoTaskCommandResult>
  {
    private readonly IMapper _mapper;
    private readonly WriteEfContext _write;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly AiConfiguration _aiConfig;
    private readonly ILogger<CreateTodoTaskCommandHandler> _logger;

    public CreateTodoTaskCommandHandler(
      IMapper mapper,
      WriteEfContext write,
      ICurrentUserService currentUserService,
      IPublishEndpoint publishEndpoint,
      AiConfiguration aiConfig,
      ILogger<CreateTodoTaskCommandHandler> logger)
    {
      _mapper = mapper;
      _write = write;
      _currentUserService = currentUserService;
      _publishEndpoint = publishEndpoint;
      _aiConfig = aiConfig;
      _logger = logger;
    }

    public async Task<CreateTodoTaskCommandResult> Handle(CreateTodoTaskCommand command, CancellationToken cancellationToken)
    {
      var todoTask = _mapper.Map<TodoTask>(command);
      todoTask.GenerateNewExternalId();
      todoTask.DueDate = DateTimeUtc.EnsureUtc(todoTask.DueDate);
      todoTask.CreatedOn = DateTime.UtcNow;
      todoTask.CreatedByUserId = TodoAccessGuard.RequireCurrentUserId(_currentUserService);

      _write.TodoTasks.Add(todoTask);

      if (_aiConfig.Features.EnableClassification)
      {
        var metadata = new TodoAiMetadata
        {
          TodoTask = todoTask,
          ClassificationStatus = ClassificationStatus.Pending,
          CreatedAt = DateTime.UtcNow,
          UpdatedAt = DateTime.UtcNow
        };
        _write.TodoAiMetadatas.Add(metadata);
        todoTask.AiMetadata = metadata;
      }

      // Bus outbox: publish then one SaveChanges so write + outbox row commit atomically.
      await _publishEndpoint.Publish(
        new TodoCreated(todoTask.ExternalId, todoTask.CreatedByUserId, DateTime.UtcNow),
        cancellationToken);

      await _write.SaveChangesAsync(cancellationToken);

      _logger.LogInformation(
        "Created todo {TodoExternalId} with transactional outbox publish",
        todoTask.ExternalId);

      return new CreateTodoTaskCommandResult()
      {
        Payload = _mapper.Map<TodoTaskDto>(todoTask)
      };
    }
  }
}
