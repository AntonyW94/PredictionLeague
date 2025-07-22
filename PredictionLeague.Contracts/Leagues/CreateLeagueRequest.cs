namespace PredictionLeague.Contracts.Leagues;

public class CreateLeagueRequest
{
    public int SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EntryCode { get; set; } = null;
    public DateTime? EntryDeadline { get; set; } = null;
}