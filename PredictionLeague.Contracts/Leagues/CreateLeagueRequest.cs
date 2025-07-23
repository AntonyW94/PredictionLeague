namespace PredictionLeague.Contracts.Leagues;

public class CreateLeagueRequest
{
    public int SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; } 
    public DateTime EntryDeadline { get; set; }
}