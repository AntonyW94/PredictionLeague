namespace PredictionLeague.Shared.Admin.Rounds;

public class RoundDto
{
    public int Id { get; init; }
    public int SeasonId { get; init; }
    public int RoundNumber { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime Deadline { get; init; }
    public int MatchCount { get; init; }
}