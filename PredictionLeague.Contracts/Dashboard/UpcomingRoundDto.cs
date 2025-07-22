namespace PredictionLeague.Contracts.Dashboard;

public class UpcomingRoundDto
{
    public int Id { get; init; }
    public string SeasonName { get; init; } = string.Empty;
    public int RoundNumber { get; init; }
    public DateTime Deadline { get; init; }
}