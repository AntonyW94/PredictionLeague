namespace ThePredictions.Contracts.Admin.Seasons;

public class BaseSeasonRequest
{
    public string Name { get; set; } = string.Empty;
    public int CompetitionId { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public bool IsActive { get; set; }
    public int NumberOfRounds { get; set; }
    public List<TournamentRoundMappingDto> TournamentRoundMappings { get; set; } = [];
}
