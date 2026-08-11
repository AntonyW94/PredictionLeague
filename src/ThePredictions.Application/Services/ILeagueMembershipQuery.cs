namespace ThePredictions.Application.Services;

/// <summary>
/// The two membership facts nearly every league query needs before it answers: is this player an approved member of
/// the league, and are they the one who runs it.
/// </summary>
/// <remarks>
/// Deliberately returns facts and never throws. What to do about a non-member is a rule, and it is not the same rule
/// everywhere: most callers want "you are not allowed", while the league dashboard answers "no such league" so that a
/// stranger cannot discover which leagues exist by reading status codes. Both live in
/// <see cref="ILeagueMembershipService"/> and its callers, not here.
/// </remarks>
public interface ILeagueMembershipQuery
{
    Task<bool> IsApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken);

    Task<bool> IsAdministratorAsync(int leagueId, string userId, CancellationToken cancellationToken);
}
