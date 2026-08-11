namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>Reads the facts behind a league's exact-scores leaderboard.</summary>
public interface IExactScoresLeaderboardQuery
{
    Task<ExactScoresLeaderboardData> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
