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
        // Information, not Warning, and the only entry in this list logged that way. Nothing has gone
        // wrong: a gate refused an account that has not confirmed its address yet, which is the gate
        // working. It is also the one refusal here that repeats - the same person hits it on every
        // attempt until they click the link - so at Warning it dominated the warnings monitor, which
        // fires on more than zero events and renotifies every 30 minutes while unresolved. The user is
        // told on screen either way: the message below is what the client displays.
        catch (EmailNotConfirmedException ex)
        {
            logger.LogInformation("Email Not Confirmed: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.Forbidden, new { message = ex.Message, emailNotConfirmed = true });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Invalid Argument/Business Rule Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { message = ex.Message });
        }
        // A rule the caller could have satisfied ("Only pending members can be approved"), so the fault is
        // the request's: 400 and a Warning. Note what is NOT caught here - InvalidOperationException falls
        // through to the unhandled bucket and is reported as an Error with a 500. That is deliberate: a
        // missing setting, a misused API or a result set that will not materialise is a server-side defect,
        // and anything nobody has classified is treated as one rather than blamed on the client.
        catch (BusinessRuleViolationException ex)
        {
            logger.LogWarning("Business Rule Error: {Message}", ex.Message);
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
        catch (OperationCanceledException)
        {
            // The request was cancelled - almost always the client disconnecting or navigating away
            // mid-request (TaskCanceledException derives from OperationCanceledException). This is not a
            // server fault, so log at Information rather than Error, and do not emit a 500 - there is
            // typically no client left to receive one.
            logger.LogInformation("Request cancelled. Request path: {Path}", context.Request.Path);
        }
        catch (Exception ex) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client aborted mid-request, but the cancellation surfaced from the data provider as a
            // SqlException ("Operation cancelled by user." / "the batch is aborted ... abort signal sent
            // from client") rather than an OperationCanceledException. Same meaning - informational, not
            // an error.
            logger.LogInformation("Request cancelled by client ({ExceptionType}). Request path: {Path}", ex.GetType().Name, context.Request.Path);
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