namespace PredictionLeague.Application.Services;

public interface ILeagueMembershipService
{
    Task<bool> IsApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken);
    Task EnsureApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken);
}
