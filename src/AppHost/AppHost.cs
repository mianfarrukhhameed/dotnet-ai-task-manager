using Aspire.Hosting;

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

// launchSettings.json on each host exposes http/https so YARP can build clusters.
// YARP (Docker) calls backends as host.docker.internal — AllowedHosts must accept that Host.
var notifications = builder
    .AddProject<Projects.NotificationService>("notifications")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("AllowedHosts", "*");

var knowledge = builder
    .AddProject<Projects.KnowledgeService>("knowledge")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq)
    .WithEnvironment("AllowedHosts", "*");

var aijobs = builder
    .AddProject<Projects.AiJobsService>("aijobs")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq)
    .WithEnvironment("AllowedHosts", "*");

var todo = builder
    .AddProject<Projects.TodoService>("todo")
    .WithReference(mainDb)
    .WithReference(rabbitmq)
    .WaitFor(mainDb)
    .WaitFor(rabbitmq)
    .WithEnvironment("AllowedHosts", "*");

// Public edge for WebApp/MCP. Host ports must match WebApp wwwroot Api:Url (WASM cannot
// read Aspire env). Prefer HTTPS :5001 so the https://localhost:5002 UI is not mixed-content blocked.
// Aspire 13.1.0 ignored WithHostHttpsPort when chained before the HTTPS endpoint existed
// (github.com/dotnet/aspire/issues/13674); 13.1.1 fixes that. BeforeStartEvent is a belt-and-suspenders pin.
#pragma warning disable ASPIRECERTIFICATES001 // experimental HTTPS developer certificate wiring for YARP
var gateway = builder
    .AddYarp("gateway")
    .WithHttpsDeveloperCertificate()
#pragma warning restore ASPIRECERTIFICATES001
    .WithHostPort(5000)
    .WithHostHttpsPort(5001)
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
    .WaitFor(todo);

builder.Eventing.Subscribe<BeforeStartEvent>((_, _) =>
{
    gateway.WithHostHttpsPort(5001);
    return Task.CompletedTask;
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
