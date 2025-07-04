namespace PredictionLeague.Core.Models;

public class LeagueMember
{
    public int LeagueId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
}