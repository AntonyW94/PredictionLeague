using PredictionLeague.Shared.Admin.Matches;

namespace PredictionLeague.Shared.Admin.Rounds;

public class RoundDetailsDto
{
    public RoundDto Round { get; init; } = new();
    public List<MatchDto> Matches { get; init; } = new();
}