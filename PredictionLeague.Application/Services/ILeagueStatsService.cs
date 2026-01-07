namespace PredictionLeague.Application.Services;

public interface ILeagueStatsService
{
    Task TakeRoundStartSnapshotsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateLiveStatsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateStableStatsAsync(int roundId, CancellationToken cancellationToken);
}