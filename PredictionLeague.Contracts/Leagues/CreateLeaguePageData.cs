using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Contracts.Leagues;

public class CreateLeaguePageData
{
    public List<SeasonDto> Seasons { get; init; } = new();
}