namespace PredictionLeague.Contracts.Admin.Seasons;

public class BaseSeasonRequest
{
    public string Name { get; set; } = string.Empty;
    public int ApiLeagueId { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public bool IsActive { get; set; }
    public int NumberOfRounds { get; set; }
}