#nullable enable

using System.Reflection;
using Fistix.TaskManager.Core.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Fistix.TaskManager.ApiHost.Shared.Extensions;

public static class SwaggerHostExtensions
{
    public static IServiceCollection AddDevelopmentSwagger(
        this IServiceCollection services,
        MasterConfig masterConfig,
        string? title = null)
    {
        var docTitle = title
            ?? masterConfig.Swagger?.Title
            ?? "API";
        var version = masterConfig.Swagger?.ApiVersion ?? "V1";

        var swaggerDescription = "";
        if (!string.IsNullOrEmpty(masterConfig.Auth0Config?.AuthClientId))
        {
            swaggerDescription =
                $"Use ClientId: <b>{masterConfig.Auth0Config.AuthClientId}</b> to authorize<br />" +
                "<u style='color: red;'>Be sure <b>openid profile email</b> is checked on the authorization popup to get user info from Auth0</u>";
        }

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(version, new OpenApiInfo
            {
                Title = docTitle,
                Version = version,
                Description = swaggerDescription
            });

            if (!string.IsNullOrWhiteSpace(masterConfig.Auth0Config?.Authority)
                && !string.IsNullOrWhiteSpace(masterConfig.Auth0Config?.Audience))
            {
                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Implicit = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri(
                                $"{masterConfig.Auth0Config.Authority}authorize?audience={masterConfig.Auth0Config.Audience}"),
                            Scopes = new Dictionary<string, string>
                            {
                                { "openid profile email", "Get all required info from Auth0" }
                            }
                        }
                    }
                });
                c.OperationFilter<SecurityRequirementsOperationFilter>();
            }

            var xmlFile = $"{Assembly.GetEntryAssembly()?.GetName().Name}.xml";
            if (!string.IsNullOrEmpty(xmlFile))
            {
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            }
        });

        return services;
    }

    public static WebApplication UseDevelopmentSwagger(
        this WebApplication app,
        MasterConfig masterConfig,
        Action<SwaggerUIOptions>? configureUi = null)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        var version = masterConfig.Swagger?.ApiVersion ?? "V1";
        var title = masterConfig.Swagger?.Title ?? "API";

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            if (configureUi != null)
            {
                configureUi(options);
            }
            else
            {
                options.SwaggerEndpoint($"/swagger/{version}/swagger.json", $"{title} {version}");
            }
        });

        return app;
    }
}
