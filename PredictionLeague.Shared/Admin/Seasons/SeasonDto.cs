namespace PredictionLeague.Shared.Admin.Seasons;

public class SeasonDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool IsActive { get; init; }
    public int RoundCount { get; init; }
}