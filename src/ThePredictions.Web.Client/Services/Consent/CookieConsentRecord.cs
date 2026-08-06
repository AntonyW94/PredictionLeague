using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Web.Client.Services.Consent;

[ExcludeFromCodeCoverage(Justification = "Browser interop: a pass-through to JavaScript with no logic of its own.")]
public class CookieConsentRecord
{
    public int Version { get; set; }
    public CookieConsentDecision Decision { get; set; }
    public DateTime TimestampUtc { get; set; }
    public bool AnalyticsAllowed { get; set; }
    public bool MarketingAllowed { get; set; }
}
