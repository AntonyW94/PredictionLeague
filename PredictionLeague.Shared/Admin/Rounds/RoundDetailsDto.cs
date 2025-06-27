using PredictionLeague.Shared.Admin.Matches;

namespace PredictionLeague.Shared.Admin.Rounds
{
    public class RoundDetailsDto
    {
        public RoundDto Round { get; set; } = new();
        public List<MatchDto> Matches { get; set; } = new();
    }
}
