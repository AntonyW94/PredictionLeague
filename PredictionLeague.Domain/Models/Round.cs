namespace PredictionLeague.Domain.Models;

public class Round
{
    public int Id { get; set; }
    public int SeasonId { get; init; }
    public int RoundNumber { get; init; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
}