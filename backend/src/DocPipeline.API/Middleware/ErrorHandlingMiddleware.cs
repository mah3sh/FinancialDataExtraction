using DocPipeline.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace DocPipeline.API.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteErrorAsync(context, ex);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title, detail) = ex switch
        {
            NotFoundException e => (HttpStatusCode.NotFound, "Not Found", e.Message),
            ValidationException e => (HttpStatusCode.BadRequest, "Validation Error", e.Message),
            UnauthorizedException e => (HttpStatusCode.Forbidden, "Forbidden", e.Message),
            InvalidOperationException e => (HttpStatusCode.Conflict, "Invalid Operation", e.Message),
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Concurrency Conflict",
                "The document was modified by another process. Please retry."),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error",
                "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status = (int)statusCode,
            detail,
            traceId = context.TraceIdentifier
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOpts));
    }
}
