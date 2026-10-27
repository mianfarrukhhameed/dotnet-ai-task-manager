using AutoMapper;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.Core;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Fistix.TaskManager.Core.DomainModel.Constants;
using Fistix.TaskManager.Core.SecurityModel;
using Fistix.TaskManager.ServiceLayer.Background;
using Fistix.TaskManager.ViewModel.Commands.Todos;
using Fistix.TaskManager.ViewModel.Dtos;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Fistix.TaskManager.ServiceLayer.Todos
{
  public class CreateTodoTaskCommandHandler : IRequestHandler<CreateTodoTaskCommand, CreateTodoTaskCommandResult>
  {
    private readonly IMapper _mapper;
    private readonly ITodoTaskRepository _todoTaskRepository;
    private readonly ITodoAiMetadataRepository _todoAiMetadataRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClassificationQueue _classificationQueue;
    private readonly IEmbeddingQueue _embeddingQueue;
    private readonly AiConfiguration _aiConfig;
    private readonly ILogger<CreateTodoTaskCommandHandler> _logger;

    public CreateTodoTaskCommandHandler(
      IMapper mapper,
      ITodoTaskRepository todoTaskRepository,
      ITodoAiMetadataRepository todoAiMetadataRepository,
      ICurrentUserService currentUserService,
      IClassificationQueue classificationQueue,
      IEmbeddingQueue embeddingQueue,
      AiConfiguration aiConfig,
      ILogger<CreateTodoTaskCommandHandler> logger)
    {
      _mapper = mapper;
      _todoTaskRepository = todoTaskRepository;
      _todoAiMetadataRepository = todoAiMetadataRepository;
      _currentUserService = currentUserService;
      _classificationQueue = classificationQueue;
      _embeddingQueue = embeddingQueue;
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
      await _todoTaskRepository.Create(todoTask, cancellationToken);

      if (_aiConfig.Features.EnableClassification)
      {
        await _todoAiMetadataRepository.MarkClassificationPendingAsync(todoTask.Id, cancellationToken);
        await _classificationQueue.EnqueueAsync(todoTask.ExternalId, cancellationToken);
        _logger.LogInformation("Queued background classification for todo {TodoExternalId}", todoTask.ExternalId);
        todoTask.AiMetadata = new TodoAiMetadata
        {
          TodoId = todoTask.Id,
          ClassificationStatus = ClassificationStatus.Pending,
          CreatedAt = DateTime.UtcNow,
          UpdatedAt = DateTime.UtcNow
        };
      }

      if (_aiConfig.Features.EnableEmbeddings)
      {
        await _embeddingQueue.EnqueueAsync(todoTask.ExternalId, cancellationToken);
        _logger.LogInformation("Queued background embedding for todo {TodoExternalId}", todoTask.ExternalId);
      }

      return new CreateTodoTaskCommandResult()
      {
        Payload = _mapper.Map<TodoTaskDto>(todoTask)
      };
    }
  }
}
