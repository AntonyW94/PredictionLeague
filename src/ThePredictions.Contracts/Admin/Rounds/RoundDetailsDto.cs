using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Rounds;

[ExcludeFromCodeCoverage]
public class RoundDetailsDto
{
    public RoundDto Round { get; init; } = null!;
    public List<MatchInRoundDto> Matches { get; init; } = [];
}
