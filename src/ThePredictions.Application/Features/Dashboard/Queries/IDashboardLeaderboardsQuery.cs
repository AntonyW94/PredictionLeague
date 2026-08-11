namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Reads every league one player belongs to, everyone in those leagues, and what they have scored.
/// </summary>
public interface IDashboardLeaderboardsQuery
{
    Task<DashboardLeaderboardsData> ExecuteAsync(string userId, CancellationToken cancellationToken);
}
