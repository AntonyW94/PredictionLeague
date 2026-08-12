namespace ThePredictions.Application.Features.Predictions.Queries;

/// <summary>
/// Reads the leagues a player is in for a season, with the boost rules those leagues run and the boosts the player has
/// already used.
/// </summary>
/// <remarks>
/// The statement this replaces answered two questions with nested <c>EXISTS</c> blocks - does this league run boosts, and has
/// this player got one left this season - the second with a <c>NOT EXISTS</c> inside an <c>EXISTS</c>. Both are rules, and
/// the rows they were built from are small: a player's leagues, those leagues' boost rules, and their own usages.
/// </remarks>
public interface IPredictionLeaguesQuery
{
    Task<PredictionLeaguesData> ExecuteAsync(string userId, int seasonId, CancellationToken cancellationToken);
}
