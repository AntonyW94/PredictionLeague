namespace PredictionLeague.Shared.Dashboard;

public class DashboardDto
{
    public List<UpcomingRoundDto> UpcomingRounds { get; init; } = new();
    public List<PublicLeagueDto> PublicLeagues { get; init; } = new();
}