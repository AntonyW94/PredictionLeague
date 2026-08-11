namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>Reads the facts behind a league's overall leaderboard.</summary>
public interface IOverallLeaderboardQuery
{
    Task<OverallLeaderboardData> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
