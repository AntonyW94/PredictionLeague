namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>Reads every league with its season and member count, for the administrator's league management screen.</summary>
/// <remarks>
/// Two rules are gone from the statement: the <c>CASE</c> that sorted each league into public, mine or somebody else's, and the
/// <c>ISNULL(l.[EntryCode], 'Public')</c> that turned a missing entry code into the word "Public" - a sentinel the screen then
/// displayed as though it were a code.
/// </remarks>
public interface IManageLeaguesQuery
{
    Task<IReadOnlyList<ManageLeagueRow>> ExecuteAsync(CancellationToken cancellationToken);
}
