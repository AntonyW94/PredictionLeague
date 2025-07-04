namespace PredictionLeague.Shared.Dashboard
{
    public class PublicLeagueDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SeasonName { get; set; } = string.Empty;
        public bool IsMember { get; set; }
    }
}
