var builder = DistributedApplication.CreateBuilder(args);

// pgvector image required — EF migrations use the Postgres vector extension.
var postgres = builder
    .AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithDataVolume("taskmanager-aspire-pgdata")
    .WithPgAdmin(pgAdmin => pgAdmin.WithHostPort(5050));

// Resource name "MainDb" injects ConnectionStrings__MainDb for WebApi MasterConfig binding.
var mainDb = postgres.AddDatabase("MainDb", databaseName: "taskdb");

// Phase 2: RabbitMQ for MassTransit + EF transactional outbox delivery.
var rabbitmq = builder
    .AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// Local OpenAI-compatible STT (Speaches / faster-whisper). CPU image + small model for better command accuracy.
// Persist HF model cache so Aspire/container restarts do not re-download every time.
var whisper = builder
    .AddContainer("whisper", "ghcr.io/speaches-ai/speaches", "latest-cpu")
    .WithHttpEndpoint(targetPort: 8000, name: "http")
    .WithEnvironment("WHISPER__MODEL", "Systran/faster-whisper-small")
    .WithVolume("taskmanager-aspire-whisper-cache", "/home/ubuntu/.cache/huggingface/hub");

// Legacy WebApi stays internal; public clients enter via YARP (Phase 4 strangler).
var webapi = builder
    .AddProject<Projects.WebApi>("webapi")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq)
    .WaitFor(whisper)
    .WithEnvironment("Ai__SpeechToText__Endpoint", whisper.GetEndpoint("http"));

// Phase 4.3: AI Worker hosts classify/embed MassTransit consumers.
builder
    .AddProject<Projects.AiWorker>("aiworker")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq);

// Phase 4.2: Notification service owns classification SignalR hub (+ more hubs later).
var notifications = builder
    .AddProject<Projects.NotificationService>("notifications")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

// Phase 4.1: YARP API Gateway — hubs/classification → Notification; everything else → WebApi.
var gateway = builder
    .AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/hubs/classification/{**catch-all}", notifications);
        yarp.AddRoute(webapi);
    })
    .WaitFor(webapi)
    .WaitFor(notifications)
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5000;
        endpoint.IsExternal = true;
    })
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 5001;
        endpoint.IsExternal = true;
    });

builder
    .AddProject<Projects.WebApp>("webapp")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.Port = 5002;
        endpoint.IsExternal = true;
    })
    .WaitFor(gateway);

builder.Build().Run();
