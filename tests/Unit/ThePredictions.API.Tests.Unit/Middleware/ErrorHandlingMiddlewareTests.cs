using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.API.Middleware;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Middleware;

/// <summary>
/// The exception-to-status mapping is a contract, not an implementation detail: it decides whether a fault
/// is alerted on as a server Error or filed as a client mistake. See ADR-0016.
/// </summary>
public class ErrorHandlingMiddlewareTests
{
    private readonly RecordingLogger<ErrorHandlingMiddleware> _logger = new();
    private readonly IWebHostEnvironment _environment = Substitute.For<IWebHostEnvironment>();

    public ErrorHandlingMiddlewareTests()
    {
        _environment.EnvironmentName.Returns("Production");
    }

    // The inversion in ADR-0016: a bare InvalidOperationException is a server-side defect (a missing
    // setting, a misused API, a result set that will not materialise), not a client mistake. If this
    // regresses to 400 and a Warning, real breakage goes back to hiding where no alert looks for it.
    [Fact]
    public async Task InvokeAsync_ShouldReturnServerErrorAndLogError_WhenInvalidOperationThrown()
    {
        var context = await InvokeWith(new InvalidOperationException("Stripe secret key is not configured."));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        LastEntry().Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnBadRequestAndLogWarning_WhenBusinessRuleViolated()
    {
        var context = await InvokeWith(new BusinessRuleViolationException("Only pending members can be approved."));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        LastEntry().Level.Should().Be(LogLevel.Warning);
        (await ReadBody(context)).RootElement.GetProperty("message").GetString()
            .Should().Be("Only pending members can be approved.");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnServerErrorAndLogError_WhenReadQueryFailed()
    {
        var materialisationFailure = new InvalidOperationException("A parameterless default constructor ... is required");

        var context = await InvokeWith(new ReadQueryFailedException(materialisationFailure));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        LastEntry().Level.Should().Be(LogLevel.Error);
    }

    // A configuration fault names infrastructure the caller has no business seeing.
    [Fact]
    public async Task InvokeAsync_ShouldHideTheMessage_WhenNotDevelopment()
    {
        var context = await InvokeWith(new InvalidOperationException("Stripe secret key is not configured."));

        var body = (await ReadBody(context)).RootElement;
        body.GetProperty("message").GetString().Should().Be("An internal server error has occurred.");
        body.GetProperty("details").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task InvokeAsync_ShouldIncludeTheMessage_WhenDevelopment()
    {
        _environment.EnvironmentName.Returns("Development");

        var context = await InvokeWith(new InvalidOperationException("Stripe secret key is not configured."));

        (await ReadBody(context)).RootElement.GetProperty("message").GetString()
            .Should().Be("Stripe secret key is not configured.");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnNotFoundAndLogWarning_WhenEntityNotFound()
    {
        var context = await InvokeWith(new EntityNotFoundException("League", 7));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        LastEntry().Level.Should().Be(LogLevel.Warning);
    }

    // ArgumentNullException derives from ArgumentException but is listed on the not-found branch, which is
    // reached first. Pinned because reordering the catch blocks would silently turn these into 400s.
    [Fact]
    public async Task InvokeAsync_ShouldReturnNotFound_WhenArgumentIsNull()
    {
        var context = await InvokeWith(new ArgumentNullException("leagueId"));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnBadRequestAndLogWarning_WhenArgumentIsInvalid()
    {
        var context = await InvokeWith(new ArgumentException("Entry code must be six characters."));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        LastEntry().Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnUnauthorisedAndHideTheMessage_WhenAccessIsDenied()
    {
        var context = await InvokeWith(new UnauthorizedAccessException("Only the administrator of league 7 can do this."));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        LastEntry().Level.Should().Be(LogLevel.Warning);
        (await ReadBody(context)).RootElement.GetProperty("message").GetString()
            .Should().Be("You are not authorised to perform this action.");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnPaymentRequiredWithSeasonId_WhenSeasonPassRequired()
    {
        var context = await InvokeWith(new SeasonPassRequiredException(12));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.PaymentRequired);
        (await ReadBody(context)).RootElement.GetProperty("seasonId").GetInt32().Should().Be(12);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnForbiddenWithFlag_WhenEmailNotConfirmed()
    {
        var context = await InvokeWith(new EmailNotConfirmedException());

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        (await ReadBody(context)).RootElement.GetProperty("emailNotConfirmed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldLogAtInformation_WhenEmailNotConfirmed()
    {
        // The one refusal in the middleware that is not a Warning. It repeats for the same person on every
        // attempt until they click the confirmation link, and at Warning it dominated the warnings monitor -
        // which alerts on more than zero events. Raising this back to Warning should fail here.
        await InvokeWith(new EmailNotConfirmedException());

        LastEntry().Level.Should().Be(LogLevel.Information);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnBadRequestWithErrors_WhenValidationFails()
    {
        var failure = new ValidationFailure("Name", "Name is required.");

        var context = await InvokeWith(new ValidationException([failure]));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        LastEntry().Level.Should().Be(LogLevel.Warning);
        (await ReadBody(context)).RootElement.GetProperty("errors").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnBadRequestWithErrors_WhenIdentityUpdateFails()
    {
        var context = await InvokeWith(new IdentityUpdateException(["Password is too short."]));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        (await ReadBody(context)).RootElement.GetProperty("errors").GetArrayLength().Should().Be(1);
    }

    // A client that navigates away is not a server fault, and there is nobody left to receive a 500.
    [Fact]
    public async Task InvokeAsync_ShouldLogInformationAndNotWrite_WhenRequestIsCancelled()
    {
        var context = await InvokeWith(new TaskCanceledException("The operation was canceled."));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        LastEntry().Level.Should().Be(LogLevel.Information);
        context.Response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_ShouldLogInformationAndNotWrite_WhenClientResetTheRequestStream()
    {
        var context = await InvokeWith(new IOException("The client reset the request stream."));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        LastEntry().Level.Should().Be(LogLevel.Information);
        context.Response.Body.Length.Should().Be(0);
    }

    // The abort can surface from the data provider as a SqlException rather than an OperationCanceledException.
    [Fact]
    public async Task InvokeAsync_ShouldLogInformation_WhenAnyExceptionFollowsAClientAbort()
    {
        using var abort = new CancellationTokenSource();
        await abort.CancelAsync();

        var context = await InvokeWith(new TimeoutException("Operation cancelled by user."), c => c.RequestAborted = abort.Token);

        LastEntry().Level.Should().Be(LogLevel.Information);
        context.Response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDoNothing_WhenTheRequestSucceeds()
    {
        var context = NewContext();
        var middleware = new ErrorHandlingMiddleware(_ => Task.CompletedTask, _logger, _environment);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        _logger.Entries.Should().BeEmpty();
        context.Response.Body.Length.Should().Be(0);
    }

    private async Task<HttpContext> InvokeWith(Exception exception, Action<HttpContext>? configure = null)
    {
        var context = NewContext();
        configure?.Invoke(context);

        var middleware = new ErrorHandlingMiddleware(_ => throw exception, _logger, _environment);
        await middleware.InvokeAsync(context);

        return context;
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/leagues/3/leaderboard/overall";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private (LogLevel Level, string Message, Exception? Exception) LastEntry()
    {
        _logger.Entries.Should().NotBeEmpty("the middleware should log every exception it handles");

        return _logger.Entries[^1];
    }

    private static async Task<JsonDocument> ReadBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
