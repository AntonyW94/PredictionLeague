using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Contracts.Leagues;

public class LeagueDashboardDto
{
    public string LeagueName { get; init; } = string.Empty;
    public List<RoundDto> ViewableRounds { get; init; } = [];
}