namespace PredictionLeague.Shared.Dashboard
{
    public class DashboardDto
    {
        public List<UpcomingRoundDto> UpcomingRounds { get; set; } = new();
        public List<PublicLeagueDto> JoinablePublicLeagues { get; set; } = new();
    }
}
