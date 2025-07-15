namespace PredictionLeague.Contracts.Dashboard;

public class PublicLeagueDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public bool IsMember { get; init; }
}