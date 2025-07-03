namespace PredictionLeague.Shared.Admin.Leagues
{
    public class LeagueDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SeasonName { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public string EntryCode { get; set; } = "Public";
    }
}
