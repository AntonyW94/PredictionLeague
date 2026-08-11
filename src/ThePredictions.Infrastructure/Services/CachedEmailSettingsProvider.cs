using Microsoft.Extensions.Caching.Memory;
using ThePredictions.Application.Services;
using DomainEmailSettings = ThePredictions.Domain.Models.EmailSettings;

namespace ThePredictions.Infrastructure.Services;

/// <summary>
/// Whether the site is sending email, cached briefly so that a switch flipped in the admin screens takes effect
/// quickly without every send checking the database.
/// </summary>
/// <remarks>
/// The caching is genuinely an Infrastructure concern and stays here. The statement it used to wrap has moved to
/// <see cref="IEmailSettingsQuery"/>, which was the last SQL in this project.
/// </remarks>
public class CachedEmailSettingsProvider(IEmailSettingsQuery settingsQuery, IMemoryCache cache)
    : IEmailSettingsProvider
{
    private const string CacheKey = "email-settings-enabled";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<bool> AreEmailsEnabledAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out bool enabled))
            return enabled;

        var stored = await settingsQuery.GetEmailsEnabledAsync(cancellationToken);

        // No row seeded yet falls back to the built-in default (emails on), matching production.
        enabled = stored ?? DomainEmailSettings.DefaultEmailsEnabled;

        cache.Set(CacheKey, enabled, CacheDuration);
        return enabled;
    }
}
