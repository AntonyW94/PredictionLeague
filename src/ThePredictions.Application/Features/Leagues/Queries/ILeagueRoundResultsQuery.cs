namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads one round's fixtures, the league's members, their predictions, their points and the boost each played.
///
/// Returns <c>null</c> when the round does not exist. The old statement joined the round and so returned no rows
/// at all in that case; the difference is that the handler now decides what an absent round means rather than
/// inheriting it from a join.
/// </summary>
public interface ILeagueRoundResultsQuery
{
    Task<LeagueRoundResultsData?> ExecuteAsync(int leagueId, int roundId, CancellationToken cancellationToken);
}
