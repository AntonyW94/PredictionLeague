using FluentValidation;
using PredictionLeague.Application.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace PredictionLeague.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
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
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument violation: {Message}", ex.Message);
          
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
          
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt: {Message}", ex.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
           
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
        catch (IdentityUpdateException ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
          
            var errors = ex.Errors.Select(e => new { e.Code, e.Description });
          
            await context.Response.WriteAsync(JsonSerializer.Serialize(errors));
        }
        catch (ValidationException ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
          
            var errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
           
            await context.Response.WriteAsync(JsonSerializer.Serialize(errors));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Entity not found: {Message}", ex.Message);
            
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred.");
          
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = _env.IsDevelopment()
            ? new { message = exception.Message, details = exception.StackTrace }
            : new { message = "An internal server error has occurred.", details = (string?)null };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}