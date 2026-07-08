using Microsoft.Extensions.Logging;

namespace ThePredictions.Web.Client.Services.Live;

/// <summary>
/// A reusable, visibility-aware polling helper. While started, it invokes a
/// refresh callback on a fixed interval (default 10s), but only while something
/// is live and the tab is visible. It pauses (skips) polls on a hidden tab and
/// stops itself once nothing is live. A failing poll keeps the last-known values
/// rather than crashing the page.
/// </summary>
public sealed class LiveScorePollingService(
    IPageVisibilityService pageVisibility,
    LivePollingOptions options,
    ILogger<LiveScorePollingService> logger) : IAsyncDisposable
{
    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private Func<bool>? _isLive;
    private Func<CancellationToken, Task>? _onPollAsync;

    public TimeSpan Interval { get; } = options.Interval;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Starts polling. Idempotent: calling <see cref="Start"/> while already
    /// running does nothing.
    /// </summary>
    /// <param name="isLive">Returns whether something is still live and worth polling.</param>
    /// <param name="onPollAsync">The refresh to run on each poll.</param>
    public void Start(Func<bool> isLive, Func<CancellationToken, Task> onPollAsync)
    {
        if (IsRunning)
            return;

        _isLive = isLive;
        _onPollAsync = onPollAsync;
        _cancellation = new CancellationTokenSource();
        IsRunning = true;
        _loop = RunLoopAsync(_cancellation.Token);
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    /// <summary>
    /// Performs a single guarded poll: skips when nothing is started, when nothing
    /// is live, or when the tab is hidden; otherwise runs the refresh callback.
    /// </summary>
    public async Task<PollOutcome> PollOnceAsync(CancellationToken cancellationToken)
    {
        if (_isLive is null || _onPollAsync is null)
            return PollOutcome.NotStarted;

        if (!_isLive())
            return PollOutcome.NotLive;

        if (pageVisibility.IsHidden)
            return PollOutcome.Hidden;

        try
        {
            await _onPollAsync(cancellationToken);
            return PollOutcome.Polled;
        }
        catch (OperationCanceledException)
        {
            return PollOutcome.Cancelled;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Live score poll failed; keeping last-known values");
            return PollOutcome.Failed;
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var outcome = await PollOnceAsync(cancellationToken);

                if (outcome == PollOutcome.NotLive)
                {
                    Stop();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped; expected on shutdown or navigation away.
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();

        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
