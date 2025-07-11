namespace PredictionLeague.Shared.Admin.Leagues;

public class UpdateLeagueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? EntryCode { get; set; }
}