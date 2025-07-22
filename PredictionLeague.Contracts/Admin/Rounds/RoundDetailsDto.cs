namespace PredictionLeague.Contracts.Admin.Rounds;

public class RoundDetailsDto
{
    public RoundDto Round { get; init; } = null!;
    public List<MatchInRoundDto> Matches { get; init; } = new();
}