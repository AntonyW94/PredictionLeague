namespace PredictionLeague.Shared.Admin.Rounds
{
    public class RoundDto
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int RoundNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime Deadline { get; set; }
        public int MatchCount { get; set; }
    }
}
