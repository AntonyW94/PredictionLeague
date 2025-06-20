namespace PredictionLeague.Core.Models;

public class GameYear
{
    public int Id { get; set; }
    public string YearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}