using System.Net;
using System.Text.Json;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no controlado");
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex switch { KeyNotFoundException => 404, ArgumentException => 400, InvalidOperationException => 400, _ => 500 };
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = context.Response.StatusCode == 500 ? "Error interno del servidor." : ex.Message }));
        }
    }
}
