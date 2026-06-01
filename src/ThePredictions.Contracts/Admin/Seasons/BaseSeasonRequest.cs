namespace ThePredictions.Contracts.Admin.Seasons;

public class BaseSeasonRequest
{
    public string Name { get; set; } = string.Empty;
    public int CompetitionId { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public bool IsActive { get; set; }
    public int NumberOfRounds { get; set; }

    /// <summary>
    /// Admin-set Standard Season Pass price. Null (or 0) means the season is free; a positive value
    /// makes the season pass-required. The Premium (SMS) tier is not yet offered.
    /// </summary>
    public decimal? PassStandardPrice { get; set; }

    public List<TournamentRoundMappingDto> TournamentRoundMappings { get; set; } = [];
}
