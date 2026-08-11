namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>Reads the facts behind a league's tournament-stage leaderboards.</summary>
public interface IStageLeaderboardQuery
{
    Task<StageLeaderboardData> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}
