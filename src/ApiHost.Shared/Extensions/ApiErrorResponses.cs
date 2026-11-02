using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fistix.TaskManager.ApiHost.Shared.Extensions;

public static class ApiErrorResponses
{
    public static ObjectResult UnexpectedError(HttpContext httpContext, string title)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = title,
            Detail = "An unexpected error occurred. Please contact support and provide the correlation ID.",
        };
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
    }
}
