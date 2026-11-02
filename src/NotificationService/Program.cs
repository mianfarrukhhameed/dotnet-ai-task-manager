using Fistix.TaskManager.NotificationService.Consumers;
using Fistix.TaskManager.NotificationService.Hubs;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var authority = builder.Configuration["Auth0:Authority"];
var audience = builder.Configuration["Auth0:Audience"];
var corsOrigins = builder.Configuration["App:CorsOrigins"]
    ?? "https://localhost:5002";

builder.Services.AddCors(options =>
{
    options.AddPolicy("webapp", policy =>
        policy.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = !string.IsNullOrWhiteSpace(audience)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TodoClassificationUpdatedConsumer>();
    x.AddConsumer<AiBatchJobUpdatedConsumer>();
    x.AddConsumer<SprintOptimizerJobUpdatedConsumer>();
    x.AddConsumer<SprintApprovalNotifyConsumer>();
    x.AddConsumer<KnowledgeIngestUpdatedConsumer>();
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

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors("webapp");
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ClassificationHub>(ClassificationHub.HubPath);
app.MapHub<AiBatchHub>(AiBatchHub.HubPath);
app.MapHub<SprintOptimizerHub>(SprintOptimizerHub.HubPath);
app.MapHub<KnowledgeIngestHub>(KnowledgeIngestHub.HubPath);

await app.RunAsync();
