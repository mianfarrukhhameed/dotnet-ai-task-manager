#nullable enable

using System;
using System.Threading.Tasks;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.Contracts.Todos;
using Fistix.TaskManager.ServiceLayer.Todos;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fistix.TaskManager.ServiceLayer.Messaging;

/// <summary>Idempotent classify consumer: re-reads DB via processor; respects Ai:Features.</summary>
public sealed class ClassifyTodoConsumer :
    IConsumer<TodoCreated>,
    IConsumer<TodoUpdated>
{
    private readonly IClassificationProcessor _classificationProcessor;
    private readonly AiConfiguration _aiConfig;
    private readonly ILogger<ClassifyTodoConsumer> _logger;

    public ClassifyTodoConsumer(
        IClassificationProcessor classificationProcessor,
        AiConfiguration aiConfig,
        ILogger<ClassifyTodoConsumer> logger)
    {
        _classificationProcessor = classificationProcessor;
        _aiConfig = aiConfig;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<TodoCreated> context)
        => ClassifyAsync(context.Message.TodoExternalId, context.CancellationToken);

    public Task Consume(ConsumeContext<TodoUpdated> context)
    {
        if (!TodoAiSideEffects.ShouldRunFromUpdated(context.Message))
        {
            return Task.CompletedTask;
        }

        return ClassifyAsync(context.Message.TodoExternalId, context.CancellationToken);
    }

    private async Task ClassifyAsync(Guid todoExternalId, System.Threading.CancellationToken cancellationToken)
    {
        if (!_aiConfig.Features.EnableClassification)
        {
            _logger.LogDebug("Skipping classify for {TodoExternalId}; feature disabled", todoExternalId);
            return;
        }

        await _classificationProcessor.ProcessQueuedAsync(todoExternalId, cancellationToken);
    }
}
