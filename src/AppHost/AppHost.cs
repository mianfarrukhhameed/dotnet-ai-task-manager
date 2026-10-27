var builder = DistributedApplication.CreateBuilder(args);

// pgvector image required — EF migrations use the Postgres vector extension.
var postgres = builder
    .AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithDataVolume("taskmanager-aspire-pgdata")
    .WithPgAdmin(pgAdmin => pgAdmin.WithHostPort(5050));

var mainDb = postgres.AddDatabase("MainDb", databaseName: "taskdb");

var rabbitmq = builder
    .AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var whisper = builder
    .AddContainer("whisper", "ghcr.io/speaches-ai/speaches", "latest-cpu")
    .WithHttpEndpoint(targetPort: 8000, name: "http")
    .WithEnvironment("WHISPER__MODEL", "Systran/faster-whisper-small")
    .WithVolume("taskmanager-aspire-whisper-cache", "/home/ubuntu/.cache/huggingface/hub");

// Legacy shell: voice STT hub + optional residual routes.
var webapi = builder
    .AddProject<Projects.WebApi>("webapi")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq)
    .WaitFor(whisper)
    .WithEnvironment("Ai__SpeechToText__Endpoint", whisper.GetEndpoint("http"));

builder
    .AddProject<Projects.AiWorker>("aiworker")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq);

var notifications = builder
    .AddProject<Projects.NotificationService>("notifications")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

var knowledge = builder
    .AddProject<Projects.KnowledgeService>("knowledge")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq);

var aijobs = builder
    .AddProject<Projects.AiJobsService>("aijobs")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq);

var todo = builder
    .AddProject<Projects.TodoService>("todo")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq);

var gateway = builder
    .AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/hubs/classification/{**catch-all}", notifications);
        yarp.AddRoute("/hubs/ai-batch/{**catch-all}", notifications);
        yarp.AddRoute("/hubs/sprint-optimizer/{**catch-all}", notifications);
        yarp.AddRoute("/hubs/knowledge-ingest/{**catch-all}", notifications);
        yarp.AddRoute("/api/ai/knowledge/{**catch-all}", knowledge);
        yarp.AddRoute("/api/ai/batch/{**catch-all}", aijobs);
        yarp.AddRoute("/api/ai/agent/sprint-optimizer/{**catch-all}", aijobs);
        yarp.AddRoute("/api/todos/{**catch-all}", todo);
        yarp.AddRoute("/api/sprints/{**catch-all}", todo);
        yarp.AddRoute("/api/ai/{**catch-all}", todo);
        yarp.AddRoute(webapi);
    })
    .WaitFor(webapi)
    .WaitFor(notifications)
    .WaitFor(knowledge)
    .WaitFor(aijobs)
    .WaitFor(todo)
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
