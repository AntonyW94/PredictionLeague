namespace PredictionLeague.Contracts.Leagues;

public class LeaguePrizesPageDto
{
    public string LeagueName { get; set; } = string.Empty;
    public DateTime EntryDeadline { get; set; }
    public decimal Price { get; set; }
    public int MemberCount { get; set; }
    public List<PrizeSettingDto> PrizeSettings { get; set; } = new();
    public int NumberOfRounds { get; set; }
    public DateTime SeasonStartDate { get; set; }
    public DateTime SeasonEndDate { get; set; }
}