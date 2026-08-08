using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;

namespace PortfolioCMS.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns a consistent
/// RFC 7807 Problem Details JSON response. No stack traces in production.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    { _logger = logger; _env = env; }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var (statusCode, title, errors) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "Validation failed",
                ve.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found", null),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", null),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message, null),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred", null)
        };

        ctx.Response.StatusCode = (int)statusCode;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status = (int)statusCode,
            detail = _env.IsDevelopment() ? exception.Message : null,
            errors
        };

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem), ct);
        return true;
    }
}
