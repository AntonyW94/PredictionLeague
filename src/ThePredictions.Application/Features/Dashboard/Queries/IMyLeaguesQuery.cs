namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Reads everything behind the My Leagues tile: the player's leagues, every round of their seasons, every score
/// posted in those leagues, and the player's cached ranks.
/// </summary>
public interface IMyLeaguesQuery
{
    Task<MyLeaguesData> ExecuteAsync(string userId, CancellationToken cancellationToken);
}
