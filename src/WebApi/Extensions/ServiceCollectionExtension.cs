using Fistix.TaskManager.WebApi.Services;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.Config;
using Fistix.TaskManager.ApiHost.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Fistix.TaskManager.Core.SecurityModel;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Fistix.TaskManager.WebApi.Extensions
{
  public static class ServiceCollectionExtension
  {
    public static void AddCommonServices(this IServiceCollection services, MasterConfig masterConfig, bool isDevelopment)
    {
      services.AddSingleton<MasterConfig>(masterConfig);

      //services.AddSingleton<ITelemetryInitializer, TelemetryRequestResponse>();

      services.AddScoped<ICurrentUserService, CurrentUserService>();
      services.AddScoped<IAccessTokenProvider, AccessTokenProvider>();

      services.AddCors(options =>
      {
        options.AddPolicy(masterConfig.AppConfig.DefaultCorsPolicyName, builder =>
              {
                builder
                          .WithOrigins(
                              masterConfig.AppConfig.CorsOrigins.Split(',').ToArray()
                          )
                          .SetIsOriginAllowedToAllowWildcardSubdomains()
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
              });
      });

      // Controllers live on Todo/Knowledge/AiJobs; WebApi hosts Swagger UI that aggregates their OpenAPI via the gateway.
      if (isDevelopment)
      {
        services.AddDevelopmentSwagger(masterConfig, title: "Task Manager (gateway)");
      }

      services
          .AddAuthentication(options =>
          {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

          })
          .AddJwtBearer(options =>
          {
            options.Authority = masterConfig.Auth0Config.Authority;
            options.Audience = masterConfig.Auth0Config.Audience;
            options.RequireHttpsMetadata = !isDevelopment;
            options.SaveToken = true;
            options.Events = new JwtBearerEvents
            {
              OnMessageReceived = context =>
              {
                // SignalR WebSockets pass the JWT as ?access_token= (not Authorization header).
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

      services.AddAuthorization(options =>
      {
        options.AddPolicy(PolicyNames.IsAdmin,
            policy => policy.RequireClaim(ClaimTypes.Role, RoleNames.Admin))
          ;
      });
    }
  }
}