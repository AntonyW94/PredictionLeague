using System.Diagnostics.CodeAnalysis;
using Blazored.LocalStorage;

namespace ThePredictions.Web.Client.Services.Consent;

// Cookie inventory (audited 2026-04-28)
//
// Essential (always set, no consent required):
//   * refreshToken      — HTTP-only auth refresh token. Set by the API after login,
//                         register, password reset and Google sign-in. Used to obtain
//                         a new JWT access token. Without it the user cannot stay
//                         signed in, so this cookie is strictly necessary.
//
// Non-essential (none currently — placeholder for future categories):
//   * Analytics         — none today. We are NOT running Google Analytics, Plausible,
//                         or any tracking. If/when added they MUST consult
//                         AnalyticsAllowed before initialising.
//   * Marketing         — none today. Same rule applies via MarketingAllowed.
//
// Other client-side storage:
//   * localStorage `accessToken`, `themePreference`, `cookieConsent` — not cookies,
//     but listed here for completeness. They are first-party functional storage and
//     do not require consent under PECR/GDPR.
//
// Bumping ConsentVersion forces every user to be re-prompted (e.g. when a new
// non-essential category is introduced or the cookie policy materially changes).
[ExcludeFromCodeCoverage(Justification = "Browser interop: a pass-through to JavaScript with no logic of its own.")]
public class ConsentBannerService(ILocalStorageService localStorage) : IConsentBannerService
{
    public const int ConsentVersion = 1;
    private const string StorageKey = "cookieConsent";

    public bool HasResponded { get; private set; }
    public CookieConsentDecision Decision { get; private set; } = CookieConsentDecision.None;
    public bool AnalyticsAllowed { get; private set; }
    public bool MarketingAllowed { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }

    public event Action? OnStateChange;

    public async Task InitialiseAsync()
    {
        var record = await localStorage.GetItemAsync<CookieConsentRecord>(StorageKey);

        if (record is null || record.Version != ConsentVersion)
        {
            HasResponded = false;
            Decision = CookieConsentDecision.None;
            AnalyticsAllowed = false;
            MarketingAllowed = false;
            RespondedAtUtc = null;
            OnStateChange?.Invoke();
            return;
        }

        HasResponded = true;
        Decision = record.Decision;
        AnalyticsAllowed = record.AnalyticsAllowed;
        MarketingAllowed = record.MarketingAllowed;
        RespondedAtUtc = record.TimestampUtc;
        OnStateChange?.Invoke();
    }

    public Task AcceptAllAsync()
    {
        return PersistAsync(CookieConsentDecision.AcceptedAll, analyticsAllowed: true, marketingAllowed: true);
    }

    public Task RejectNonEssentialAsync()
    {
        return PersistAsync(CookieConsentDecision.RejectedNonEssential, analyticsAllowed: false, marketingAllowed: false);
    }

    public Task SaveCustomPreferencesAsync(bool analyticsAllowed, bool marketingAllowed)
    {
        var decision = analyticsAllowed && marketingAllowed
            ? CookieConsentDecision.AcceptedAll
            : !analyticsAllowed && !marketingAllowed
                ? CookieConsentDecision.RejectedNonEssential
                : CookieConsentDecision.CustomPreferences;

        return PersistAsync(decision, analyticsAllowed, marketingAllowed);
    }

    public async Task ReopenAsync()
    {
        await localStorage.RemoveItemAsync(StorageKey);
        HasResponded = false;
        Decision = CookieConsentDecision.None;
        AnalyticsAllowed = false;
        MarketingAllowed = false;
        RespondedAtUtc = null;
        OnStateChange?.Invoke();
    }

    private async Task PersistAsync(CookieConsentDecision decision, bool analyticsAllowed, bool marketingAllowed)
    {
        var record = new CookieConsentRecord
        {
            Version = ConsentVersion,
            Decision = decision,
            TimestampUtc = DateTime.UtcNow,
            AnalyticsAllowed = analyticsAllowed,
            MarketingAllowed = marketingAllowed
        };

        await localStorage.SetItemAsync(StorageKey, record);

        HasResponded = true;
        Decision = decision;
        AnalyticsAllowed = analyticsAllowed;
        MarketingAllowed = marketingAllowed;
        RespondedAtUtc = record.TimestampUtc;
        OnStateChange?.Invoke();
    }
}
