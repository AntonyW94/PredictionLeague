namespace PredictionLeague.Core.Models;

public class GameWeek
{
    public int Id { get; set; }
    public int GameYearId { get; set; }
    public int WeekNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
}