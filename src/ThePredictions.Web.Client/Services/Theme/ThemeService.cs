using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using ThePredictions.Contracts.Account;

namespace ThePredictions.Web.Client.Services.Theme;

[ExcludeFromCodeCoverage(Justification = "Browser interop: a pass-through to JavaScript with no logic of its own.")]
public class ThemeService(ILocalStorageService localStorage, HttpClient httpClient) : IThemeService
{
    private const string StorageKey = "themePreference";

    public string CurrentTheme { get; private set; } = "light";
    public bool IsDarkMode => CurrentTheme == "dark";
    public event Action? OnThemeChanged;

    public async Task InitialiseAsync()
    {
        var cached = await localStorage.GetItemAsync<string>(StorageKey);
        if (!string.IsNullOrEmpty(cached))
        {
            CurrentTheme = cached;
            OnThemeChanged?.Invoke();
        }
    }

    public async Task ToggleThemeAsync()
    {
        CurrentTheme = IsDarkMode ? "light" : "dark";
        await localStorage.SetItemAsync(StorageKey, CurrentTheme);
        OnThemeChanged?.Invoke();

        try
        {
            await httpClient.PutAsJsonAsync("api/account/theme", CurrentTheme);
        }
        catch
        {
            // Fire-and-forget — localStorage is the primary store for instant UX
        }
    }

    public async Task ToggleThemeAsGuestAsync()
    {
        CurrentTheme = IsDarkMode ? "light" : "dark";
        await localStorage.SetItemAsync(StorageKey, CurrentTheme);
        OnThemeChanged?.Invoke();
    }

    public async Task ClearPreferenceAsync()
    {
        CurrentTheme = "light";
        await localStorage.RemoveItemAsync(StorageKey);
        OnThemeChanged?.Invoke();
    }

    public async Task SyncOnLoginAsync()
    {
        var localTheme = await localStorage.GetItemAsync<string>(StorageKey);

        if (!string.IsNullOrEmpty(localTheme))
        {
            // Local choice wins — push it to the server so the account reflects it.
            try
            {
                await httpClient.PutAsJsonAsync("api/account/theme", localTheme);
            }
            catch
            {
                // Fire-and-forget — next toggle will retry
            }
            return;
        }

        // No local preference (e.g. fresh device) — adopt the server's saved theme.
        try
        {
            var details = await httpClient.GetFromJsonAsync<UserDetails>("api/account/details");
            if (details is null || string.IsNullOrEmpty(details.PreferredTheme))
                return;

            CurrentTheme = details.PreferredTheme;
            await localStorage.SetItemAsync(StorageKey, CurrentTheme);
            OnThemeChanged?.Invoke();
        }
        catch
        {
            // Best-effort — leave default in place
        }
    }
}
