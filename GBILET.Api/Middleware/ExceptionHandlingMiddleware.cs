using System.Net;
using System.Text.Json;

namespace GBILET.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception. Path={Path} Method={Method}",
                context.Request.Path, context.Request.Method);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = JsonSerializer.Serialize(new { error = "Internal server error" });
            await context.Response.WriteAsync(response);
        }
    }
}
