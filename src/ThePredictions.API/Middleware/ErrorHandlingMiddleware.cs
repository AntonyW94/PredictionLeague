using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Domain.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace ThePredictions.API.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentNullException or EntityNotFoundException)
        {
            logger.LogWarning("Not Found Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.NotFound, new { message = ex.Message });
        }
        catch (SeasonPassRequiredException ex)
        {
            logger.LogWarning("Season Pass Required: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.PaymentRequired, new { message = ex.Message, seasonId = ex.SeasonId });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Invalid Argument/Business Rule Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Invalid Operation Error: {Message}", ex.Message);
             await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { message = ex.Message });
        }
        catch (FluentValidation.ValidationException ex)
        {
            logger.LogWarning("Validation Error: {Errors}", ex.Errors);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { errors = ex.Errors });
        }
        catch (IdentityUpdateException ex)
        {
            logger.LogWarning("Identity Update Error: {Message}", ex.Errors);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { errors = ex.Errors });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Authorization Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.Unauthorized, new { message = "You are not authorised to perform this action." });
        }
        catch (IOException ex) when (ex.Message.Contains("The client reset the request stream"))
        {
            logger.LogInformation("Client reset the request stream. Request path: {Path}", context.Request.Path);
        } 
        catch (Exception ex) when (ex.Message.Contains("A task was canceled"))
        {
            logger.LogInformation("Task cancelled. Request path: {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception has occurred.");
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

        var response = env.IsDevelopment()
            ? new { message = exception.Message, details = exception.StackTrace }
            : new { message = "An internal server error has occurred.", details = (string?)null };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}