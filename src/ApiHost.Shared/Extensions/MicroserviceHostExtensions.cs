#nullable enable

using System.Security.Claims;
using System.Threading.RateLimiting;
using Fistix.TaskManager.AiLayer.Abstractions;
using Fistix.TaskManager.AiLayer.Agents;
using Fistix.TaskManager.AiLayer.Implementations;
using Fistix.TaskManager.AiLayer.Observability;
using Fistix.TaskManager.AiLayer.Shared;
using Fistix.TaskManager.AiLayer.Tools;
using Fistix.TaskManager.ApiHost.Shared.Services;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.Config;
using Fistix.TaskManager.Core.SecurityModel;
using Fistix.TaskManager.DataLayer;
using Fistix.TaskManager.ServiceLayer;
using Fistix.TaskManager.ServiceLayer.Notifications;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Fistix.TaskManager.ApiHost.Shared.Extensions;

public static class MicroserviceHostExtensions
{
    public static MasterConfig BindMasterConfig(this IConfiguration configuration)
    {
        return new MasterConfig
        {
            AppConfig = configuration.GetSection("App").Get<AppConfig>() ?? new AppConfig(),
            Auth0Config = configuration.GetSection("Auth0").Get<Auth0Config>() ?? new Auth0Config(),
            ConnectionString = configuration.GetSection("ConnectionStrings").Get<ConnectionStringConfig>()
                ?? new ConnectionStringConfig(),
            Swagger = configuration.GetSection("Swagger").Get<SwaggerConfig>() ?? new SwaggerConfig(),
            AzureConfig = configuration.GetSection("Azure").Get<AzureConfig>() ?? new AzureConfig()
        };
    }

    public static IServiceCollection AddMicroserviceCommon(
        this IServiceCollection services,
        MasterConfig masterConfig,
        IConfiguration configuration,
        ServiceLayerHostOptions workers,
        bool enableBusOutbox,
        bool enableBusNotifiers,
        Action<IBusRegistrationConfigurator>? configureBus = null,
        bool enableDelayedMessageScheduler = false)
    {
        services.AddSingleton(masterConfig);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();

        var corsOrigins = masterConfig.AppConfig?.CorsOrigins ?? "https://localhost:5002";
        var corsName = string.IsNullOrWhiteSpace(masterConfig.AppConfig?.DefaultCorsPolicyName)
            ? "default"
            : masterConfig.AppConfig.DefaultCorsPolicyName;

        services.AddCors(options =>
        {
            options.AddPolicy(corsName, builder =>
                builder.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = masterConfig.Auth0Config.Authority;
                options.Audience = masterConfig.Auth0Config.Audience;
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
            });

        services.AddAuthorization();
        services.AddServiceLayer(masterConfig, workers);
        services.AddAiCoreServices(configuration);

        if (enableBusOutbox || enableBusNotifiers || configureBus != null)
        {
            services.AddMassTransit(x =>
            {
                if (enableBusOutbox)
                {
                    x.AddEntityFrameworkOutbox<WriteEfContext>(o =>
                    {
                        o.QueryDelay = TimeSpan.FromSeconds(1);
                        o.UsePostgres();
                        o.UseBusOutbox();
                    });
                }

                if (enableDelayedMessageScheduler)
                {
                    x.AddDelayedMessageScheduler();
                }

                configureBus?.Invoke(x);

                x.SetKebabCaseEndpointNameFormatter();
                x.UsingRabbitMq((context, cfg) =>
                {
                    var cs = configuration.GetConnectionString("rabbitmq")
                        ?? configuration.GetConnectionString("RabbitMQ")
                        ?? "amqp://guest:guest@localhost:5672";
                    cfg.Host(cs);
                    if (enableDelayedMessageScheduler)
                    {
                        cfg.UseDelayedMessageScheduler();
                    }

                    cfg.ConfigureEndpoints(context);
                });
            });
        }

        if (enableBusNotifiers)
        {
            services.RemoveAll<IClassificationNotifier>();
            services.RemoveAll<IAiBatchNotifier>();
            services.RemoveAll<ISprintOptimizerNotifier>();
            services.RemoveAll<IKnowledgeIngestNotifier>();
            services.AddSingleton<IClassificationNotifier, BusClassificationNotifier>();
            services.AddSingleton<IAiBatchNotifier, BusAiBatchNotifier>();
            services.AddSingleton<ISprintOptimizerNotifier, BusSprintOptimizerNotifier>();
            services.AddSingleton<IKnowledgeIngestNotifier, BusKnowledgeIngestNotifier>();
        }

        services.AddAiRateLimiting(configuration);
        return services;
    }

    public static IServiceCollection AddAiCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ =>
        {
            var aiConfig = new AiConfiguration();
            configuration.GetSection("Ai").Bind(aiConfig);
            return aiConfig;
        });

        services.RemoveAll<IAiTelemetry>();
        services.AddSingleton<IAiTelemetry, AiTelemetry>();
        services.AddSingleton<SemanticKernelOrchestrator>();
        services.AddSingleton(provider =>
        {
            var orchestrator = provider.GetRequiredService<SemanticKernelOrchestrator>();
            return orchestrator.CreateKernelAsync().GetAwaiter().GetResult();
        });
        services.AddSingleton<SemanticKernelLlmProvider>();
        services.AddSingleton<ILlmProviderService>(sp =>
            new ObservingLlmProvider(
                sp.GetRequiredService<SemanticKernelLlmProvider>(),
                sp.GetRequiredService<IAiTelemetry>(),
                sp.GetRequiredService<AiConfiguration>()));
        services.AddSingleton<AiChatClientFactory>();
        services.AddScoped<SummarizationPipeline>();
        services.AddScoped<ClassificationPipeline>();
        services.AddScoped<SemanticSearchPipeline>();
        services.AddScoped<RAGPipeline>();
        services.AddScoped<ToolProposalPipeline>();
        services.AddSingleton<TodoManagementPlugin>();
        services.AddHttpClient(nameof(SemanticKernelEmbeddingService));

        var embeddingProvider = configuration["Ai:Embedding:Provider"] ?? "Onnx";
        if (string.Equals(embeddingProvider, "onnx", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<OnnxBgeEmbeddingService>();
            services.AddSingleton<IEmbeddingService>(sp =>
                new ObservingEmbeddingService(
                    sp.GetRequiredService<OnnxBgeEmbeddingService>(),
                    sp.GetRequiredService<IAiTelemetry>()));
        }
        else
        {
            services.AddScoped<SemanticKernelEmbeddingService>();
            services.AddScoped<IEmbeddingService>(sp =>
                new ObservingEmbeddingService(
                    sp.GetRequiredService<SemanticKernelEmbeddingService>(),
                    sp.GetRequiredService<IAiTelemetry>()));
        }

        return services;
    }

    public static IServiceCollection AddAiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var aiConfig = new AiConfiguration();
        configuration.GetSection("Ai").Bind(aiConfig);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Title = "Too many requests",
                    Detail = "AI rate limit exceeded. Please try again later.",
                    Status = StatusCodes.Status429TooManyRequests
                }, cancellationToken);
            };

            AddFixedWindowPolicy(options, RateLimitPolicies.AiSummarize, aiConfig.Features.SummarizeRateLimit);
            AddFixedWindowPolicy(options, RateLimitPolicies.AiClassify, aiConfig.Features.ClassifyRateLimit);
            AddFixedWindowPolicy(options, RateLimitPolicies.AiSemanticSearch, aiConfig.Features.SemanticSearchRateLimit);
            AddFixedWindowPolicy(options, RateLimitPolicies.AiRag, aiConfig.Features.RagRateLimit);
            AddFixedWindowPolicy(options, RateLimitPolicies.AiKnowledgeRag, aiConfig.Features.KnowledgeRagRateLimit);
            AddFixedWindowPolicy(options, RateLimitPolicies.AiFunctionCalling, aiConfig.Features.FunctionCallingRateLimit);
            AddFixedWindowPolicy(options, RateLimitPolicies.AiAgents, aiConfig.Features.AgentsRateLimit);
            AddFixedWindowPolicy(options, RateLimitPolicies.AiTranscribe, aiConfig.Features.VoiceTranscriptionRateLimit);
        });

        return services;
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options,
        string policyName,
        AiRateLimitConfiguration rateLimit)
    {
        if (!rateLimit.Enabled)
        {
            options.AddPolicy(policyName, _ => RateLimitPartition.GetNoLimiter(policyName));
            return;
        }

        var permitLimit = Math.Max(1, rateLimit.PermitLimit);
        var window = TimeSpan.FromMinutes(Math.Max(1, rateLimit.WindowMinutes));

        options.AddPolicy(policyName, httpContext =>
        {
            var partitionKey = httpContext.User?.FindFirstValue("sub")
                ?? httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });
    }

    public static WebApplication UseMicroservicePipeline(this WebApplication app, MasterConfig masterConfig)
    {
        var corsName = string.IsNullOrWhiteSpace(masterConfig.AppConfig?.DefaultCorsPolicyName)
            ? "default"
            : masterConfig.AppConfig.DefaultCorsPolicyName;

        app.UseAuthentication();
        app.UseCors(corsName);
        app.Use(async (context, next) =>
        {
            var userIdentity = context.User.Identities.FirstOrDefault();
            if (userIdentity != null)
            {
                var currentUserService = context.RequestServices.GetService(typeof(ICurrentUserService)) as ICurrentUserService;
                if (currentUserService?.HasAdminProfile == true)
                    userIdentity.AddClaim(new Claim(ClaimTypes.Role, RoleNames.Admin));
            }

            await next();
        });
        app.UseAuthorization();
        app.UseRateLimiter();
        return app;
    }
}
