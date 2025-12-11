namespace PredictionLeague.Contracts.Predictions;

public class PredictionLeagueDto
{
    public int LeagueId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool HasBoosts { get; init; } = false;
}