namespace ThePredictions.Application.Services;

/// <summary>
/// Guards eighteen league queries: you must be an approved member to read a league, and its administrator to change
/// how it runs.
/// </summary>
/// <remarks>
/// Moved out of Infrastructure, where it held its own two SQL statements and was excluded from coverage as
/// "repository composition over SQL". The composition was the interesting part - the reads are now
/// <see cref="ILeagueMembershipQuery"/> and what to do about the answer is tested here.
/// </remarks>
public class LeagueMembershipService(ILeagueMembershipQuery membershipQuery) : ILeagueMembershipService
{
    public Task<bool> IsApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken) =>
        membershipQuery.IsApprovedMemberAsync(leagueId, userId, cancellationToken);

    public async Task EnsureApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        var isMember = await IsApprovedMemberAsync(leagueId, userId, cancellationToken);

        if (!isMember)
            throw new UnauthorizedAccessException("You must be a member of this league to access this resource.");
    }

    public Task<bool> IsLeagueAdministratorAsync(int leagueId, string userId, CancellationToken cancellationToken) =>
        membershipQuery.IsAdministratorAsync(leagueId, userId, cancellationToken);

    public async Task EnsureLeagueAdministratorAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        var isAdmin = await IsLeagueAdministratorAsync(leagueId, userId, cancellationToken);

        if (!isAdmin)
            throw new UnauthorizedAccessException("Only the league administrator can access this resource.");
    }
}
