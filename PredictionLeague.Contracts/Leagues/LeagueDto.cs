namespace PredictionLeague.Contracts.Leagues;

public class LeagueDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SeasonName { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public string EntryCode { get; init; } = "Public";
}