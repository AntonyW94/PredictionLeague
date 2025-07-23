namespace PredictionLeague.Contracts.Leagues;

public class UpdateLeagueRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime EntryDeadline { get; set; }
}