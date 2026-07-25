using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class SiteSettings
{
    // Public site root (e.g. https://www.thepredictions.co.uk). Used to build absolute links in emails
    // sent from background jobs, where there is no request Origin to derive the URL from.
    public string? BaseUrl { get; set; }

    // Canonical fallback used only when BaseUrl is not configured. Kept in one place so every email link
    // builder resolves the same value. Never derived from a request header (that is attacker-controllable).
    public const string FallbackBaseUrl = "https://www.thepredictions.co.uk";

    // The site root to build links from: the configured BaseUrl with any trailing slash removed, or the
    // fallback when BaseUrl is blank. Always returns a value with no trailing slash.
    public string ResolvedBaseUrl =>
        string.IsNullOrWhiteSpace(BaseUrl)
            ? FallbackBaseUrl
            : BaseUrl.TrimEnd('/');
}
