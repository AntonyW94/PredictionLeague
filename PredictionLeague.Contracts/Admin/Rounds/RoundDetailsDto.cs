using PredictionLeague.Contracts.Admin.Matches;

namespace PredictionLeague.Contracts.Admin.Rounds;

public class RoundDetailsDto
{
    public RoundDto Round { get; init; } = new();
    public List<MatchDto> Matches { get; init; } = new();
}