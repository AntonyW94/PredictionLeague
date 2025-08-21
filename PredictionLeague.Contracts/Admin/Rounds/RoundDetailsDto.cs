namespace PredictionLeague.Contracts.Admin.Rounds;

public class RoundDetailsDto
{
    public RoundWithAllPredictionsInDto Round { get; init; } = null!;
    public List<MatchInRoundDto> Matches { get; init; } = new();
}