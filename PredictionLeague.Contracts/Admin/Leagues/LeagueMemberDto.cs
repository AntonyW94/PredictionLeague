namespace PredictionLeague.Contracts.Admin.Leagues;

public class LeagueMemberDto
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool CanBeApproved { get; set; }
}