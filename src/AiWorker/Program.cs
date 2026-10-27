using Fistix.TaskManager.AiLayer.Abstractions;
using Fistix.TaskManager.AiLayer.Agents;
using Fistix.TaskManager.AiLayer.Implementations;
using Fistix.TaskManager.AiLayer.Observability;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.AiLayer.Tools;
using Fistix.TaskManager.Core.Config;
using Fistix.TaskManager.DataLayer;
using Fistix.TaskManager.ServiceLayer;
using Fistix.TaskManager.ServiceLayer.Messaging;
using Fistix.TaskManager.ServiceLayer.Notifications;
using MassTransit;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var masterConfig = new MasterConfig
{
    ConnectionString = builder.Configuration.GetSection("ConnectionStrings").Get<ConnectionStringConfig>()
        ?? new ConnectionStringConfig(),
    AppConfig = builder.Configuration.GetSection("App").Get<AppConfig>() ?? new AppConfig(),
    Auth0Config = builder.Configuration.GetSection("Auth0").Get<Auth0Config>() ?? new Auth0Config(),
    Swagger = builder.Configuration.GetSection("Swagger").Get<SwaggerConfig>() ?? new SwaggerConfig(),
    AzureConfig = builder.Configuration.GetSection("Azure").Get<AzureConfig>() ?? new AzureConfig()
};

builder.Services.AddServiceLayer(masterConfig);
builder.Services.TryAddSingleton<IClassificationNotifier, NullClassificationNotifier>();

builder.Services.AddSingleton(_ =>
{
    var aiConfig = new AiConfiguration();
    builder.Configuration.GetSection("Ai").Bind(aiConfig);
    return aiConfig;
});
builder.Services.RemoveAll<IAiTelemetry>();
builder.Services.AddSingleton<IAiTelemetry, AiTelemetry>();
builder.Services.AddSingleton<SemanticKernelOrchestrator>();
builder.Services.AddSingleton(provider =>
{
    var orchestrator = provider.GetRequiredService<SemanticKernelOrchestrator>();
    return orchestrator.CreateKernelAsync().GetAwaiter().GetResult();
});
builder.Services.AddSingleton<SemanticKernelLlmProvider>();
builder.Services.AddSingleton<ILlmProviderService>(sp =>
    new ObservingLlmProvider(
        sp.GetRequiredService<SemanticKernelLlmProvider>(),
        sp.GetRequiredService<IAiTelemetry>(),
        sp.GetRequiredService<AiConfiguration>()));
builder.Services.AddSingleton<AiChatClientFactory>();
builder.Services.AddScoped<SummarizationPipeline>();
builder.Services.AddScoped<ClassificationPipeline>();
builder.Services.AddScoped<SemanticSearchPipeline>();
builder.Services.AddScoped<RAGPipeline>();
builder.Services.AddSingleton<TodoManagementPlugin>();
builder.Services.AddHttpClient(nameof(SemanticKernelEmbeddingService));

var embeddingProvider = builder.Configuration["Ai:Embedding:Provider"] ?? "Onnx";
if (string.Equals(embeddingProvider, "onnx", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<OnnxBgeEmbeddingService>();
    builder.Services.AddSingleton<IEmbeddingService>(sp =>
        new ObservingEmbeddingService(
            sp.GetRequiredService<OnnxBgeEmbeddingService>(),
            sp.GetRequiredService<IAiTelemetry>()));
}
else
{
    builder.Services.AddScoped<SemanticKernelEmbeddingService>();
    builder.Services.AddScoped<IEmbeddingService>(sp =>
        new ObservingEmbeddingService(
            sp.GetRequiredService<SemanticKernelEmbeddingService>(),
            sp.GetRequiredService<IAiTelemetry>()));
}

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ClassifyTodoConsumer>();
    x.AddConsumer<EmbedTodoConsumer>();

    // Inbox for idempotent consume; no bus outbox (worker does not publish from HTTP UoW).
    x.AddEntityFrameworkOutbox<WriteEfContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.UsePostgres();
    });

    x.SetKebabCaseEndpointNameFormatter();

    x.UsingRabbitMq((context, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("rabbitmq")
            ?? builder.Configuration.GetConnectionString("RabbitMQ")
            ?? "amqp://guest:guest@localhost:5672";

        cfg.Host(connectionString);
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();

if (builder.Environment.IsDevelopment())
{
    Fistix.TaskManager.DataLayer.Startup.ApplyMigrations(host.Services);
}

await host.RunAsync();
