using ThePredictions.Web.Client.Services.Live;

namespace ThePredictions.Web.Client.Tests.Unit.TestDoubles;

/// <summary>
/// A controllable <see cref="IPageVisibilityService"/> for tests: flip
/// <see cref="IsHidden"/> to simulate the tab being hidden or shown.
/// </summary>
public sealed class FakePageVisibilityService : IPageVisibilityService
{
    private bool _isHidden;

    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (_isHidden == value)
                return;

            _isHidden = value;
            VisibilityChanged?.Invoke();
        }
    }

    public event Action? VisibilityChanged;

    public Task InitialiseAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
