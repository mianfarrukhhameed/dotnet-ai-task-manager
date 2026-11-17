using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.Config;
using Fistix.TaskManager.Core.SecurityModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Security.Claims;

namespace Fistix.TaskManager.WebApi.Extensions
{
  public static class ApplicationBuilderExtension
  {
    public static void UseCommonService(
      this IApplicationBuilder app,
      MasterConfig masterConfig,
      IWebHostEnvironment environment)
    {
      if (environment.IsDevelopment())
      {
        // OpenAPI JSON for API hosts is proxied by YARP under /openapi/{service}/… → /swagger/…
        // Use https://localhost:5001/swagger (gateway), not the residual WebApi port alone.
        var version = masterConfig.Swagger?.ApiVersion ?? "V1";
        app.UseSwagger();
        app.UseSwaggerUI(x =>
        {
          x.SwaggerEndpoint($"/openapi/todo/{version}/swagger.json", "Todo / AI");
          x.SwaggerEndpoint($"/openapi/knowledge/{version}/swagger.json", "Knowledge Lab");
          x.SwaggerEndpoint($"/openapi/aijobs/{version}/swagger.json", "AI Jobs");
          x.DocumentTitle = "Task Manager API";
        });
      }

      app.UseAuthentication();

      app.UseCors(masterConfig.AppConfig.DefaultCorsPolicyName);

      app.Use(async (context, next) =>
      {
        var userIdentity = context.User.Identities.FirstOrDefault();
        if (userIdentity != null)
        {
          ICurrentUserService currentUserService =
              (ICurrentUserService)context.RequestServices.GetService(typeof(ICurrentUserService));

          if (currentUserService.HasAdminProfile)
            userIdentity.AddClaim(new Claim(ClaimTypes.Role, RoleNames.Admin));
        }

        await next.Invoke();
      });

      app.UseAuthorization();

      app.UseRateLimiter();
    }
  }
}