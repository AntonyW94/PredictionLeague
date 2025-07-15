namespace PredictionLeague.Contracts.Admin.Seasons;

public class CreateSeasonRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}