using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThePredictions.API.Filters;
using Xunit;

namespace ThePredictions.API.Tests.Unit.Filters;

/// <summary>
/// The only thing protecting the scheduled-task endpoints, which publish rounds, send emails and
/// update scores. A hole here lets anyone on the internet trigger them.
/// </summary>
public class ApiKeyAuthoriseAttributeTests
{
    private const string HeaderName = "X-Api-Key";
    private const string ConfiguredKey = "the-real-scheduler-key";

    private static ActionExecutingContext BuildContext(string? configuredKey, string? suppliedKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FootballApi:SchedulerApiKey"] = configuredKey })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (suppliedKey is not null)
            httpContext.Request.Headers[HeaderName] = suppliedKey;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());

        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: null!);
    }

    private static async Task<(ActionExecutingContext Context, bool ActionRan)> RunAsync(string? configuredKey, string? suppliedKey)
    {
        var context = BuildContext(configuredKey, suppliedKey);
        var actionRan = false;

        await new ApiKeyAuthoriseAttribute().OnActionExecutionAsync(context, () =>
        {
            actionRan = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        return (context, actionRan);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldRunTheAction_WhenTheKeyMatches()
    {
        var (context, actionRan) = await RunAsync(ConfiguredKey, ConfiguredKey);

        actionRan.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReject_WhenTheHeaderIsMissing()
    {
        var (context, actionRan) = await RunAsync(ConfiguredKey, suppliedKey: null);

        actionRan.Should().BeFalse();
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReject_WhenTheKeyIsWrong()
    {
        var (context, actionRan) = await RunAsync(ConfiguredKey, "not-the-key");

        actionRan.Should().BeFalse();
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReject_WhenTheSuppliedKeyIsADifferentLength()
    {
        // Exercises the length-mismatch path, which still burns a comparison so the response time
        // does not reveal how much of the key was right.
        var (context, actionRan) = await RunAsync(ConfiguredKey, "short");

        actionRan.Should().BeFalse();
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReject_WhenTheKeyDiffersOnlyInCase()
    {
        var (context, actionRan) = await RunAsync(ConfiguredKey, ConfiguredKey.ToUpperInvariant());

        actionRan.Should().BeFalse();
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task OnActionExecutionAsync_ShouldRejectEveryCall_WhenNoKeyIsConfigured(string? configuredKey)
    {
        // Fail closed: a missing setting must lock the endpoints, never open them.
        var (context, actionRan) = await RunAsync(configuredKey, "anything");

        actionRan.Should().BeFalse();
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReject_WhenTheHeaderIsEmpty()
    {
        var (context, actionRan) = await RunAsync(ConfiguredKey, string.Empty);

        actionRan.Should().BeFalse();
        context.Result.Should().BeOfType<UnauthorizedResult>();
    }
}
