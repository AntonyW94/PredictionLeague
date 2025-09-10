namespace PredictionLeague.Contracts.Leagues;

public class DefinePrizeStructureRequest
{
    public List<DefinePrizeSettingDto> PrizeSettings { get; init; } = [];
}