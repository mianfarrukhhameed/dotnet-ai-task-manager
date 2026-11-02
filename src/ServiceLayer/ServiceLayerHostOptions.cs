namespace Fistix.TaskManager.ServiceLayer;

/// <summary>Controls which background workers a host process starts (Phase 4 multi-host).</summary>
public sealed class ServiceLayerHostOptions
{
    public bool EnableEmbeddingBackfill { get; set; } = true;
    public bool EnableAiBatchWorker { get; set; } = true;
    public bool EnableSprintOptimizerWorker { get; set; } = true;
    public bool EnableKnowledgeIngestWorker { get; set; } = true;

    public static ServiceLayerHostOptions AllWorkers { get; } = new();

    public static ServiceLayerHostOptions None { get; } = new()
    {
        EnableEmbeddingBackfill = false,
        EnableAiBatchWorker = false,
        EnableSprintOptimizerWorker = false,
        EnableKnowledgeIngestWorker = false
    };

    public static ServiceLayerHostOptions KnowledgeOnly { get; } = new()
    {
        EnableEmbeddingBackfill = false,
        EnableAiBatchWorker = false,
        EnableSprintOptimizerWorker = false,
        EnableKnowledgeIngestWorker = true
    };

    public static ServiceLayerHostOptions AiJobsOnly { get; } = new()
    {
        EnableEmbeddingBackfill = false,
        EnableAiBatchWorker = true,
        EnableSprintOptimizerWorker = true,
        EnableKnowledgeIngestWorker = false
    };

    public static ServiceLayerHostOptions TodoOnly { get; } = new()
    {
        EnableEmbeddingBackfill = true,
        EnableAiBatchWorker = false,
        EnableSprintOptimizerWorker = false,
        EnableKnowledgeIngestWorker = false
    };
}
