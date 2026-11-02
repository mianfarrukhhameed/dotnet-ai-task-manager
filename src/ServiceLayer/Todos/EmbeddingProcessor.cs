#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.AiLayer.Abstractions;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.Core.Abstractions.Repositories;
using Microsoft.Extensions.Logging;

namespace Fistix.TaskManager.ServiceLayer.Todos;

public interface IEmbeddingProcessor
{
    Task ProcessQueuedAsync(Guid todoExternalId, CancellationToken cancellationToken);
    Task BackfillMissingAsync(CancellationToken cancellationToken);
}

public sealed class EmbeddingProcessor : IEmbeddingProcessor
{
    private readonly ITodoTaskRepository _todoTaskRepository;
    private readonly ITodoEmbeddingRepository _todoEmbeddingRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly AiConfiguration _aiConfig;
    private readonly ILogger<EmbeddingProcessor> _logger;

    public EmbeddingProcessor(
        ITodoTaskRepository todoTaskRepository,
        ITodoEmbeddingRepository todoEmbeddingRepository,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        AiConfiguration aiConfig,
        ILogger<EmbeddingProcessor> logger)
    {
        _todoTaskRepository = todoTaskRepository;
        _todoEmbeddingRepository = todoEmbeddingRepository;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _aiConfig = aiConfig;
        _logger = logger;
    }

    public async Task ProcessQueuedAsync(Guid todoExternalId, CancellationToken cancellationToken)
    {
        if (!_aiConfig.Features.EnableEmbeddings)
        {
            return;
        }

        var todo = await _todoTaskRepository.Get(todoExternalId, cancellationToken);
        var text = BuildEmbeddingText(todo.Title, todo.Description);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            text,
            EmbeddingInputKind.Passage,
            cancellationToken);
        await _vectorStore.UpsertTodoEmbeddingAsync(
            todo.Id,
            embedding,
            _embeddingService.ModelName,
            cancellationToken);

        _logger.LogInformation("Stored embedding for todo {TodoExternalId}", todoExternalId);
    }

    public async Task BackfillMissingAsync(CancellationToken cancellationToken)
    {
        if (!_aiConfig.Features.EnableEmbeddings)
        {
            return;
        }

        var missing = await _todoEmbeddingRepository.GetTodoExternalIdsMissingEmbeddingsAsync(
            _embeddingService.ModelName,
            cancellationToken);

        _logger.LogInformation("Embedding backfill queued for {Count} todos", missing.Count);

        foreach (var id in missing)
        {
            try
            {
                await ProcessQueuedAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding backfill failed for todo {TodoExternalId}", id);
            }
        }
    }

    public static string BuildEmbeddingText(string? title, string? description)
    {
        var t = title?.Trim() ?? string.Empty;
        var d = description?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(d) ? t : $"{t}\n{d}";
    }
}
