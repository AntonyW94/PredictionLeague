namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// Reads one round with the season and competition it belongs to, or nothing if there is no such round.
/// </summary>
/// <remarks>
/// Shared by the prediction page and the share card, which each joined these three tables themselves. Whether the round is
/// the last of its season, and whether the competition is a tournament, are rules and are not decided here.
/// </remarks>
public interface IRoundHeaderQuery
{
    Task<RoundHeaderRow?> ExecuteAsync(int roundId, CancellationToken cancellationToken);
}
