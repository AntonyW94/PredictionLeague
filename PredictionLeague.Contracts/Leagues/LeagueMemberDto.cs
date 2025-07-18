namespace PredictionLeague.Contracts.Leagues;

public class LeagueMemberDto
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool CanBeApproved { get; init; }
}