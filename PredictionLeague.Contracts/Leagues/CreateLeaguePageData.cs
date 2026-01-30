using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Contracts.Leagues;

public class CreateLeaguePageData
{
    public List<SeasonLookupDto> Seasons { get; init; } = [];
    public int DefaultPointsForExactScore { get; init; }
    public int DefaultPointsForCorrectResult { get; init; }
}