using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class SiteSettings
{
    // Public site root (e.g. https://www.thepredictions.co.uk). Used to build absolute links in emails
    // sent from background jobs, where there is no request Origin to derive the URL from.
    public string? BaseUrl { get; set; }
}
