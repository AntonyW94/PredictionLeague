namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>
/// Reads every account and every badge ever awarded, for the site-wide badges table.
/// </summary>
/// <remarks>
/// Deliberately unfiltered and ungrouped. Who counts as a player, what a player's badge total is, and the order
/// they stand in are all rules, and all three used to be decided in SQL - twice over, because the tile worked out
/// the same player's position with a second statement that did not agree with the table's.
/// </remarks>
public interface IBadgeLeaderboardQuery
{
    Task<BadgeLeaderboardData> ExecuteAsync(CancellationToken cancellationToken);
}
