using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public class LeagueMemberDto
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
    public LeagueMemberStatus Status { get; init; }
    public bool CanBeApproved { get; init; }
}