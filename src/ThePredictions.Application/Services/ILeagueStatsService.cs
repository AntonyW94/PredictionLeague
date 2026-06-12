namespace ThePredictions.Application.Services;

public interface ILeagueStatsService
{
    Task TakeRoundStartSnapshotsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateLiveStatsAsync(int roundId, CancellationToken cancellationToken);
    Task UpdateStableStatsAsync(int roundId, CancellationToken cancellationToken);

    /// <summary>
    /// Ensures every approved member of every league in the round's season has a
    /// cached stats row. Returns the number of rows created.
    /// </summary>
    Task<int> EnsureMemberStatsRowsExistAsync(int roundId, CancellationToken cancellationToken);
}