namespace PredictionLeague.Shared.Admin.Leagues
{
    public class LeagueMemberDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
