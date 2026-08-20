using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Domain.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace ThePredictions.API.Middleware;

/// <summary>
/// Turns an exception into a status code and a log entry.
/// </summary>
/// <remarks>
/// <b>Severity says who has to act, not how the request ended.</b> Every branch below except the last is a request the
/// caller could have made differently - a wrong id, a failed validation, a rule the current state does not allow, an
/// account that has not confirmed its address - and none of them needs anybody to look at anything. Those are logged at
/// <c>Information</c>: still there to read when investigating one person's problem, invisible to alerting.
///
/// <c>Warning</c> is reserved for the things that do want acting on - a slow query, a missing index, a third party that
/// has stopped answering - and none of them are exceptions the caller caused, so none of them are in this file.
/// <c>Error</c> is the last branch: an exception nobody classified, which is a defect until proven otherwise.
///
/// This is what makes the warnings monitor worth reading. It fires on more than zero warnings in five minutes and
/// renotifies every 30 minutes while unresolved, so a bucket that also held routine refusals could not be alerted on -
/// and a real warning arriving among them would not be noticed. See ADR-0018.
/// </remarks>
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
            logger.LogInformation("Not Found Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.NotFound, new { message = ex.Message });
        }
        catch (SeasonPassRequiredException ex)
        {
            logger.LogInformation("Season Pass Required: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.PaymentRequired, new { message = ex.Message, seasonId = ex.SeasonId });
        }
        // The refusal that made the case for the policy above: the same account trips this gate on every
        // attempt until it clicks the confirmation link, so at Warning one unconfirmed player could keep
        // the alerts channel busy indefinitely. The person is told either way - the message below is what
        // the client puts on screen.
        catch (EmailNotConfirmedException ex)
        {
            logger.LogInformation("Email Not Confirmed: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.Forbidden, new { message = ex.Message, emailNotConfirmed = true });
        }
        catch (ArgumentException ex)
        {
            logger.LogInformation("Invalid Argument/Business Rule Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { message = ex.Message });
        }
        // A rule the caller could have satisfied ("Only pending members can be approved"), so the fault is
        // the request's: 400 and an Information. Note what is NOT caught here - InvalidOperationException
        // falls through to the unhandled bucket and is reported as an Error with a 500. That is deliberate:
        // a missing setting, a misused API or a result set that will not materialise is a server-side
        // defect, and anything nobody has classified is treated as one rather than blamed on the client.
        catch (BusinessRuleViolationException ex)
        {
            logger.LogInformation("Business Rule Error: {Message}", ex.Message);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { message = ex.Message });
        }
        catch (FluentValidation.ValidationException ex)
        {
            logger.LogInformation("Validation Error: {Errors}", ex.Errors);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { errors = ex.Errors });
        }
        catch (IdentityUpdateException ex)
        {
            logger.LogInformation("Identity Update Error: {Message}", ex.Errors);
            await HandleKnownExceptionAsync(context, HttpStatusCode.BadRequest, new { errors = ex.Errors });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogInformation("Authorization Error: {Message}", ex.Message);
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