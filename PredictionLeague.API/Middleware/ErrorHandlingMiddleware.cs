using PredictionLeague.Application.Common.Exceptions;
using PredictionLeague.Domain.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace PredictionLeague.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentNullException or EntityNotFoundException)
        {
            _logger.LogWarning("Not Found Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.NotFound, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid Argument/Business Rule Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid Operation Error: {Message}", ex.Message);
             await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { message = ex.Message });
        }
        catch (FluentValidation.ValidationException ex)
        {
            _logger.LogWarning("Validation Error: {Errors}", ex.Errors);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { errors = ex.Errors });
        }
        catch (IdentityUpdateException ex)
        {
            _logger.LogWarning("Identity Update Error: {Message}", ex.Errors);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { errors = ex.Errors.Select(e => e.Description) });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Authorization Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.Unauthorized, new { message = "You are not authorized to perform this action." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred.");
            await HandleUnhandledExceptionAsync(context, ex);
        }
    }

    private static Task HandleKnownExceptionAsync(HttpContext context, HttpStatusCode statusCode, object errorResponse)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    private Task HandleUnhandledExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = _env.IsDevelopment()
            ? new { message = exception.Message, details = exception.StackTrace }
            : new { message = "An internal server error has occurred.", details = (string?)null };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}