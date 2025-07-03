namespace PredictionLeague.Shared.Dashboard
{
    public class UpcomingRoundDto
    {
        public int Id { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public int RoundNumber { get; set; }
        public DateTime Deadline { get; set; }
        public List<MatchPredictionDto> Matches { get; set; } = new();
    }
}
