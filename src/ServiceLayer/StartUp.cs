using Fistix.TaskManager.AiLayer.Abstractions;
using Fistix.TaskManager.AiLayer.Observability;
using Fistix.TaskManager.Core.AutoMapperProfiles;
using Fistix.TaskManager.Core.Config;
using Fistix.TaskManager.DataLayer;
using Fistix.TaskManager.ServiceLayer.Background;
using Fistix.TaskManager.ServiceLayer.Knowledge;
using Fistix.TaskManager.ServiceLayer.Notifications;
using Fistix.TaskManager.ServiceLayer.Todos;
using Fistix.TaskManager.ViewModel.Commands.Todos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fistix.TaskManager.ServiceLayer
{
  public static class StartUp
  {
    public static void AddServiceLayer(
      this IServiceCollection services,
      MasterConfig masterConfig,
      ServiceLayerHostOptions? hostOptions = null)
    {
      var workers = hostOptions ?? ServiceLayerHostOptions.AllWorkers;

      services.AddMediatR(typeof(CreateTodoTaskCommand).Assembly, typeof(CreateTodoTaskCommandHandler).Assembly);

      services.AddAutoMapper(x=>x.AddProfile<TodoTaskProfileMapping>());

        services.AddScoped<IToolExecutor, ToolExecutor>();
      services.AddScoped<SprintPlanningTools>();
      services.AddScoped<SprintCandidateLoader>();
      services.AddScoped<SprintOptimizerCheckpointService>();
      services.AddScoped<SprintOptimizerWorkflowHost>();
      services.AddScoped<SprintOptimizerAgent>();
      services.AddScoped<SprintOptimizerPersistService>();

      services.AddScoped<IClassificationProcessor, ClassificationProcessor>();
      services.TryAddSingleton<IClassificationNotifier, NullClassificationNotifier>();
      services.AddScoped<IEmbeddingProcessor, EmbeddingProcessor>();
      services.AddScoped<IVectorStore, PgVectorEmbeddingStore>();
      services.AddScoped<ILexicalTodoSearch, PostgresLexicalTodoSearch>();
      if (workers.EnableEmbeddingBackfill)
      {
        services.AddHostedService<EmbeddingBackfillHostedService>();
      }

      services.AddScoped<IAiBatchStepExecutor, AiBatchStepExecutor>();
      if (workers.EnableAiBatchWorker)
      {
        services.AddHostedService<AiBatchBackgroundService>();
      }
      // SignalR/bus notifiers registered by hosts; tests register Null*.
      services.TryAddSingleton<IAiBatchNotifier, NullAiBatchNotifier>();
      services.TryAddSingleton<ISprintOptimizerNotifier, NullSprintOptimizerNotifier>();
      services.TryAddSingleton<IKnowledgeIngestNotifier, NullKnowledgeIngestNotifier>();
      services.AddScoped<IKnowledgeIngestProcessor, KnowledgeIngestProcessor>();
      services.AddScoped<KnowledgeSemanticSearchPipeline>();
      services.AddScoped<KnowledgeQueryRewriter>();
      if (workers.EnableKnowledgeIngestWorker)
      {
        services.AddHostedService<KnowledgeIngestBackgroundService>();
      }
      services.TryAddSingleton<IAiTelemetry>(_ => NullAiTelemetry.Instance);
      if (workers.EnableSprintOptimizerWorker)
      {
        services.AddHostedService<SprintOptimizerBackgroundService>();
      }
            
      services.AddDataLayer(masterConfig);
    }
  }
}
