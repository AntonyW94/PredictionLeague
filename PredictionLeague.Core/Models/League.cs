namespace PredictionLeague.Core.Models
{
    public class League
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SeasonId { get; set; }
        public string AdministratorUserId { get; set; } = string.Empty;
        public string? EntryCode { get; set; } // Null for public leagues
        public DateTime CreatedAt { get; set; }
    }
}