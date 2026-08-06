using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage]
public class CreateCheckoutSessionRequest
{
    public int SeasonId { get; set; }
}
