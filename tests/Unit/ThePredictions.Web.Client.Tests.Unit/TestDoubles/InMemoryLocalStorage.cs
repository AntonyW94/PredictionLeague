using Blazored.LocalStorage;

namespace ThePredictions.Web.Client.Tests.Unit.TestDoubles;

/// <summary>
/// A stateful in-memory <see cref="ILocalStorageService"/> for tests. Only the
/// members the auth code uses (get / set / remove by key) are implemented; the
/// rest throw so an unexpected dependency is obvious.
/// </summary>
public sealed class InMemoryLocalStorage : ILocalStorageService
{
    private readonly Dictionary<string, object?> _store = new();

    public ValueTask<T?> GetItemAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = _store.TryGetValue(key, out var stored) ? (T?)stored : default;
        return ValueTask.FromResult(value);
    }

    public ValueTask SetItemAsync<T>(string key, T data, CancellationToken cancellationToken = default)
    {
        _store[key] = data;
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ContainKeyAsync(string key, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_store.ContainsKey(key));

    // Unused by the code under test.
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public ValueTask<string?> GetItemAsStringAsync(string key, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public ValueTask<string?> KeyAsync(int index, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public ValueTask<IEnumerable<string>> KeysAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public ValueTask<int> LengthAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public ValueTask RemoveItemsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public ValueTask SetItemAsStringAsync(string key, string data, CancellationToken cancellationToken = default) => throw new NotImplementedException();

#pragma warning disable CS0067 // Events are required by the interface but unused in tests.
    public event EventHandler<ChangingEventArgs>? Changing;
    public event EventHandler<ChangedEventArgs>? Changed;
#pragma warning restore CS0067
}
