using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Rounds;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class RoundDetailsDto
{
    public RoundDto Round { get; init; } = null!;
    public List<MatchInRoundDto> Matches { get; init; } = [];
}
