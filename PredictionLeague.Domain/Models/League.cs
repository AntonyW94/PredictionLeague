namespace PredictionLeague.Domain.Models;

public class League
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeasonId { get; init; }
    public string AdministratorUserId { get; init; } = string.Empty;
    public string? EntryCode { get; set; }
    public DateTime CreatedAt { get; init; }
}