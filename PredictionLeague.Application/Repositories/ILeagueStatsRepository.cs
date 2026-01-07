namespace PredictionLeague.Application.Repositories;

public interface ILeagueStatsRepository
{
    Task SnapshotRanksForRoundStartAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateLiveStatsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateStableStatsAsync(int roundId, CancellationToken cancellationToken);
}