namespace PredictionLeague.Contracts.Leagues;

public class LeaguePrizesPageDto
{
    public string LeagueName { get; init; } = string.Empty;
    public DateTime EntryDeadlineUtc { get; init; }
    public decimal Price { get; init; }
    public int MemberCount { get; init; }
    public List<PrizeSettingDto> PrizeSettings { get; init; } = [];
    public int NumberOfRounds { get; init; }
    public DateTime SeasonStartDateUtc { get; init; }
    public DateTime SeasonEndDateUtc { get; init; }
}