using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class StripeSettings
{
    public const string SectionName = "Stripe";

    public string? SecretKey { get; init; }
    public string? WebhookSecret { get; init; }
    public string? ProductId { get; init; }
}
