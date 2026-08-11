namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// Reads the fixtures in one round, with their teams and results.
/// </summary>
/// <remarks>
/// Shared by the administrator's round editor and the players' round view, which had a statement each: the same twenty
/// columns and the same two joins, written out twice and differing in three ways. One of them left out postponed
/// fixtures, one ordered by kick-off, and one declared the joined team columns as never-null when a placeholder fixture
/// makes every one of them null. All three differences were decided in SQL and only one of them was intended.
///
/// Every fixture comes back, in no order. Which ones a screen shows and how they are sorted are rules.
/// </remarks>
public interface IRoundMatchesQuery
{
    Task<IReadOnlyList<RoundMatchRow>> ExecuteAsync(int roundId, CancellationToken cancellationToken);
}
