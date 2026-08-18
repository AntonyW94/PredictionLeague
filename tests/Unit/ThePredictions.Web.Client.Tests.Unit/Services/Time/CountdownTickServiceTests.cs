using FluentAssertions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using NSubstitute;
using ThePredictions.Web.Client.Services.Time;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Services.Time;

/// <summary>
/// The one-second heartbeat every countdown on the page shares.
/// </summary>
/// <remarks>
/// What matters here is that the browser interval is started once and stopped once, however many countdowns
/// come and go. The Active Rounds card renders a lock countdown per match and two cards can sit side by side,
/// so a timer per countdown would have meant dozens of intervals each calling into .NET every second.
/// </remarks>
public class CountdownTickServiceTests
{
    private const string Start = "blazorInterop.startCountdown";
    private const string Stop = "blazorInterop.stopCountdown";

    private readonly IJSRuntime _jsRuntime = Substitute.For<IJSRuntime>();
    private readonly CountdownTickService _service;

    public CountdownTickServiceTests()
    {
        _service = new CountdownTickService(_jsRuntime);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldStartTheInterval_WhenTheFirstHandlerArrives()
    {
        await _service.SubscribeAsync(() => { });

        StartCalls().Should().Be(1);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldNotStartASecondInterval_WhenMoreHandlersArrive()
    {
        await _service.SubscribeAsync(() => { });
        await _service.SubscribeAsync(() => { });
        await _service.SubscribeAsync(() => { });

        StartCalls().Should().Be(1);
    }

    [Fact]
    public async Task OnTick_ShouldCallEverySubscriber()
    {
        var first = 0;
        var second = 0;

        await _service.SubscribeAsync(() => first++);
        await _service.SubscribeAsync(() => second++);

        _service.OnTick();
        _service.OnTick();

        first.Should().Be(2);
        second.Should().Be(2);
    }

    [Fact]
    public async Task OnTick_ShouldNotCallAHandlerThatHasUnsubscribed()
    {
        var ticks = 0;
        Action handler = () => ticks++;

        await _service.SubscribeAsync(handler);
        _service.OnTick();

        await _service.UnsubscribeAsync(handler);
        _service.OnTick();

        ticks.Should().Be(1);
    }

    [Fact]
    public async Task OnTick_ShouldSurviveAHandlerThatUnsubscribesItself()
    {
        // A countdown reaching zero tears itself down from inside its own handler, which would otherwise be
        // mutating the list being walked.
        var ticks = 0;
        Action? handler = null;

        handler = () =>
        {
            ticks++;
            _ = _service.UnsubscribeAsync(handler!);
        };

        await _service.SubscribeAsync(handler);

        var tick = () => _service.OnTick();

        tick.Should().NotThrow();
        ticks.Should().Be(1);
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldStopTheInterval_WhenTheLastHandlerLeaves()
    {
        var first = () => { };
        var second = () => { };

        await _service.SubscribeAsync(first);
        await _service.SubscribeAsync(second);

        await _service.UnsubscribeAsync(first);
        StopCalls().Should().Be(0, "another countdown is still listening");

        await _service.UnsubscribeAsync(second);
        StopCalls().Should().Be(1);
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldDoNothing_ForAHandlerThatWasNeverSubscribed()
    {
        await _service.SubscribeAsync(() => { });

        await _service.UnsubscribeAsync(() => { });

        StopCalls().Should().Be(0);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldRestartTheInterval_WhenACountdownReturnsAfterTheLastOneLeft()
    {
        var handler = () => { };

        await _service.SubscribeAsync(handler);
        await _service.UnsubscribeAsync(handler);
        await _service.SubscribeAsync(handler);

        StartCalls().Should().Be(2);
    }

    [Fact]
    public async Task DisposeAsync_ShouldStopTheInterval_WhenCountdownsAreStillSubscribed()
    {
        await _service.SubscribeAsync(() => { });

        await _service.DisposeAsync();

        StopCalls().Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotStopAnInterval_WhenNothingWasEverSubscribed()
    {
        await _service.DisposeAsync();

        StopCalls().Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_ShouldSwallowADisconnectedBrowser()
    {
        // Teardown races the circuit going away; there is no interval left to clear, and throwing here would
        // surface as an unhandled exception during disposal.
        _jsRuntime
            .InvokeAsync<IJSVoidResult>(Stop, Arg.Any<object?[]?>())
            .Returns<ValueTask<IJSVoidResult>>(_ => throw new JSDisconnectedException("gone"));

        await _service.SubscribeAsync(() => { });

        var dispose = async () => await _service.DisposeAsync();

        await dispose.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeSafeToCallTwice()
    {
        await _service.SubscribeAsync(() => { });

        await _service.DisposeAsync();
        await _service.DisposeAsync();

        StopCalls().Should().Be(1);
    }

    private int StartCalls() => CallsTo(Start);

    private int StopCalls() => CallsTo(Stop);

    // InvokeVoidAsync is an extension over InvokeAsync<IJSVoidResult>, so that is what the substitute records.
    private int CallsTo(string identifier) =>
        _jsRuntime
            .ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IJSRuntime.InvokeAsync)
                && call.GetArguments().FirstOrDefault() as string == identifier);
}
