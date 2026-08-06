using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Web.Client.Services.Live;
using ThePredictions.Web.Client.Tests.Unit.TestDoubles;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Services.Live;

public class LiveScorePollingServiceTests
{
    private readonly FakePageVisibilityService _visibility = new();
    private readonly ILogger<LiveScorePollingService> _logger = Substitute.For<ILogger<LiveScorePollingService>>();

    private LiveScorePollingService CreateService(TimeSpan? interval = null) =>
        new(_visibility, new LivePollingOptions { Interval = interval ?? TimeSpan.FromSeconds(10) }, _logger);

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(10, CancellationToken.None);
        }
    }

    [Fact]
    public void Interval_ShouldDefaultTo10Seconds_WhenOptionsNotConfigured()
    {
        new LivePollingOptions().Interval.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Interval_ShouldReflectConfiguredValue_WhenOptionsSet()
    {
        var service = CreateService(TimeSpan.FromSeconds(5));

        service.Interval.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PollOnceAsync_ShouldReturnNotStarted_WhenNeverStarted()
    {
        await using var service = CreateService();

        var outcome = await service.PollOnceAsync(CancellationToken.None);

        outcome.Should().Be(PollOutcome.NotStarted);
    }

    [Fact]
    public async Task Start_ShouldSetIsRunning_AndStopShouldClearIt()
    {
        await using var service = CreateService();

        service.Start(() => true, _ => Task.CompletedTask);
        service.IsRunning.Should().BeTrue();

        service.Stop();
        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Start_ShouldBeIdempotent_WhenAlreadyRunning()
    {
        await using var service = CreateService();

        service.Start(() => true, _ => Task.CompletedTask);
        var act = () => service.Start(() => true, _ => Task.CompletedTask);

        act.Should().NotThrow();
        service.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task PollOnceAsync_ShouldInvokeCallback_WhenLiveAndVisible()
    {
        await using var service = CreateService();
        var count = 0;
        service.Start(() => true, _ =>
        {
            count++;
            return Task.CompletedTask;
        });

        var outcome = await service.PollOnceAsync(CancellationToken.None);

        outcome.Should().Be(PollOutcome.Polled);
        count.Should().Be(1);
    }

    [Fact]
    public async Task PollOnceAsync_ShouldSkip_WhenTabHidden()
    {
        _visibility.IsHidden = true;
        await using var service = CreateService();
        var count = 0;
        service.Start(() => true, _ =>
        {
            count++;
            return Task.CompletedTask;
        });

        var outcome = await service.PollOnceAsync(CancellationToken.None);

        outcome.Should().Be(PollOutcome.Hidden);
        count.Should().Be(0);
    }

    [Fact]
    public async Task PollOnceAsync_ShouldNotPoll_WhenNothingLive()
    {
        await using var service = CreateService();
        var count = 0;
        service.Start(() => false, _ =>
        {
            count++;
            return Task.CompletedTask;
        });

        var outcome = await service.PollOnceAsync(CancellationToken.None);

        outcome.Should().Be(PollOutcome.NotLive);
        count.Should().Be(0);
    }

    [Fact]
    public async Task PollOnceAsync_ShouldReturnFailed_WhenCallbackThrows()
    {
        await using var service = CreateService();
        service.Start(() => true, _ => throw new InvalidOperationException("poll failed"));

        var outcome = await service.PollOnceAsync(CancellationToken.None);

        outcome.Should().Be(PollOutcome.Failed);
    }

    [Fact]
    public async Task Loop_ShouldPollRepeatedly_WhenLiveAndVisible()
    {
        await using var service = CreateService(TimeSpan.FromMilliseconds(30));
        var count = 0;
        service.Start(() => true, _ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        await WaitUntilAsync(() => Volatile.Read(ref count) >= 2, TimeSpan.FromSeconds(2));

        Volatile.Read(ref count).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Loop_ShouldStopItself_WhenNothingLive()
    {
        var service = CreateService(TimeSpan.FromMilliseconds(30));
        service.Start(() => false, _ => Task.CompletedTask);

        await WaitUntilAsync(() => !service.IsRunning, TimeSpan.FromSeconds(2));

        // Await the loop task itself rather than ending the test while it unwinds, so the
        // self-stop path has definitely finished.
        await service.DisposeAsync();

        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Loop_ShouldPauseWhileHidden_AndResumeWhenVisible()
    {
        _visibility.IsHidden = true;
        await using var service = CreateService(TimeSpan.FromMilliseconds(30));
        var count = 0;
        service.Start(() => true, _ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        await Task.Delay(150, CancellationToken.None);
        Volatile.Read(ref count).Should().Be(0, "polling is paused while the tab is hidden");
        service.IsRunning.Should().BeTrue("a hidden tab pauses polling rather than stopping it");

        _visibility.IsHidden = false;
        await WaitUntilAsync(() => Volatile.Read(ref count) >= 1, TimeSpan.FromSeconds(2));

        Volatile.Read(ref count).Should().BeGreaterThanOrEqualTo(1, "polling resumes when the tab becomes visible");
    }

    [Fact]
    public async Task PollOnceAsync_ShouldReturnCancelled_WhenTheCallbackIsCancelled()
    {
        // Navigating away cancels the token mid-poll. That is an orderly stop, not a failure, so
        // it must not be logged as one.
        await using var service = CreateService();
        service.Start(() => true, _ => throw new OperationCanceledException());

        var outcome = await service.PollOnceAsync(CancellationToken.None);

        outcome.Should().Be(PollOutcome.Cancelled);
        _logger.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == LogLevel.Warning)
            .Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_ShouldWaitForTheLoopToFinish()
    {
        var service = CreateService(TimeSpan.FromMilliseconds(20));
        service.Start(() => true, _ => Task.CompletedTask);

        await service.DisposeAsync();

        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeSafe_WhenPollingWasNeverStarted()
    {
        var service = CreateService();

        var act = async () => await service.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeSafe_WhenCalledTwice()
    {
        var service = CreateService(TimeSpan.FromMilliseconds(20));
        service.Start(() => true, _ => Task.CompletedTask);

        await service.DisposeAsync();
        var act = async () => await service.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
