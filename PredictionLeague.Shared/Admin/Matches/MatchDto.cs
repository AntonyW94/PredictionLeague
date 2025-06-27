namespace PredictionLeague.Shared.Admin.Matches
{
    public class MatchDto
    {
        public int Id { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public DateTime MatchDateTime { get; set; }
    }
}
