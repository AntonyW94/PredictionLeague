namespace PredictionLeague.Contracts.Leagues;

public class LeaguePrizesPageDto
{
    public string LeagueName { get; init; } = string.Empty;
    public DateTime EntryDeadline { get; init; }
    public decimal Price { get; init; }
    public int MemberCount { get; init; }
    public List<PrizeSettingDto> PrizeSettings { get; init; } = [];
    public int NumberOfRounds { get; init; }
    public DateTime SeasonStartDate { get; init; }
    public DateTime SeasonEndDate { get; init; }
}