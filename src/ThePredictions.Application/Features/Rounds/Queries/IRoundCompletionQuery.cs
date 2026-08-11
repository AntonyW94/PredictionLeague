namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// Reads a round, its fixtures, its participants and their predictions.
///
/// Returns <c>null</c> when the round does not exist. When <paramref name="leagueId"/> is null the
/// participants are every approved member of every league in the round's season; when set they are that
/// league's approved members only.
/// </summary>
public interface IRoundCompletionQuery
{
    Task<RoundCompletionData?> ExecuteAsync(int roundId, int? leagueId, CancellationToken cancellationToken);
}
