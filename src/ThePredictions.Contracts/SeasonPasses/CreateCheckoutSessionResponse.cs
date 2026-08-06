using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage]
public record CreateCheckoutSessionResponse(string CheckoutUrl);
