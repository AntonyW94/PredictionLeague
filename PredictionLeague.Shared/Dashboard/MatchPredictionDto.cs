namespace PredictionLeague.Shared.Dashboard
{
    public class MatchPredictionDto
    {
        public int MatchId { get; set; }
        public DateTime MatchDateTime { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public string HomeTeamLogoUrl { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
        public string AwayTeamLogoUrl { get; set; } = string.Empty;
        public int? PredictedHomeScore { get; set; }
        public int? PredictedAwayScore { get; set; }
    }
}
