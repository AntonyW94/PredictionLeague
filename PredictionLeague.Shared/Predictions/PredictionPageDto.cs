using PredictionLeague.Shared.Dashboard;

namespace PredictionLeague.Shared.Predictions
{
    public class PredictionPageDto
    {
        public int RoundId { get; set; }
        public int RoundNumber { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public DateTime Deadline { get; set; }
        public bool IsPastDeadline { get; set; }
        public List<MatchPredictionDto> Matches { get; set; } = new();
    }
}
