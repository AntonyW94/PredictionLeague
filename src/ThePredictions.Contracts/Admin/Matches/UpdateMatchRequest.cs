using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Matches;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class UpdateMatchRequest : BaseMatchRequest
{
    public int Id { get; init; }
}
