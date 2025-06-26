namespace PredictionLeague.Core.Models;

public class Round
{
    public int Id { get; set; }
    public int GameYearId { get; set; }
    public int RoundNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
}