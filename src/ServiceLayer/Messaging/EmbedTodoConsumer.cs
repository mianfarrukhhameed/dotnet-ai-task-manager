#nullable enable

using System;
using System.Threading.Tasks;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.Contracts.Todos;
using Fistix.TaskManager.ServiceLayer.Todos;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fistix.TaskManager.ServiceLayer.Messaging;

/// <summary>Idempotent embed consumer: upserts via processor; respects Ai:Features.</summary>
public sealed class EmbedTodoConsumer :
    IConsumer<TodoCreated>,
    IConsumer<TodoUpdated>
{
    private readonly IEmbeddingProcessor _embeddingProcessor;
    private readonly AiConfiguration _aiConfig;
    private readonly ILogger<EmbedTodoConsumer> _logger;

    public EmbedTodoConsumer(
        IEmbeddingProcessor embeddingProcessor,
        AiConfiguration aiConfig,
        ILogger<EmbedTodoConsumer> logger)
    {
        _embeddingProcessor = embeddingProcessor;
        _aiConfig = aiConfig;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<TodoCreated> context)
        => EmbedAsync(context.Message.TodoExternalId, context.CancellationToken);

    public Task Consume(ConsumeContext<TodoUpdated> context)
    {
        if (!TodoAiSideEffects.ShouldRunFromUpdated(context.Message))
        {
            return Task.CompletedTask;
        }

        return EmbedAsync(context.Message.TodoExternalId, context.CancellationToken);
    }

    private async Task EmbedAsync(Guid todoExternalId, System.Threading.CancellationToken cancellationToken)
    {
        if (!_aiConfig.Features.EnableEmbeddings)
        {
            _logger.LogDebug("Skipping embed for {TodoExternalId}; feature disabled", todoExternalId);
            return;
        }

        await _embeddingProcessor.ProcessQueuedAsync(todoExternalId, cancellationToken);
    }
}
