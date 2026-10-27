#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.ServiceLayer.Todos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fistix.TaskManager.ServiceLayer.Background;

/// <summary>One-shot startup backfill for missing embeddings (replaces channel-queue drain loop).</summary>
public sealed class EmbeddingBackfillHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AiConfiguration _aiConfig;
    private readonly ILogger<EmbeddingBackfillHostedService> _logger;

    public EmbeddingBackfillHostedService(
        IServiceScopeFactory scopeFactory,
        AiConfiguration aiConfig,
        ILogger<EmbeddingBackfillHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _aiConfig = aiConfig;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_aiConfig.Features.EnableEmbeddings)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IEmbeddingProcessor>();
            await processor.BackfillMissingAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Embedding backfill on startup failed");
        }
    }
}
