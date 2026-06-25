using Microsoft.Extensions.Caching.Memory;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using DomainEmailSettings = ThePredictions.Domain.Models.EmailSettings;

namespace ThePredictions.Infrastructure.Services;

public class CachedEmailSettingsProvider(IApplicationReadDbConnection dbConnection, IMemoryCache cache)
    : IEmailSettingsProvider
{
    private const string CacheKey = "email-settings-enabled";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<bool> AreEmailsEnabledAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out bool enabled))
            return enabled;

        const string sql = @"
            SELECT TOP 1
                es.[EmailsEnabled]
            FROM
                [EmailSettings] es
            ORDER BY
                es.[Id];";

        var stored = await dbConnection.QuerySingleOrDefaultAsync<bool?>(sql, cancellationToken);

        // No row seeded yet falls back to the built-in default (emails on), matching production.
        enabled = stored ?? DomainEmailSettings.DefaultEmailsEnabled;

        cache.Set(CacheKey, enabled, CacheDuration);
        return enabled;
    }
}
