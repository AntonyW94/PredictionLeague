using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;

namespace ThePredictions.Infrastructure.Services;

public class LeagueStatsService(ILeagueStatsRepository statsRepository) : ILeagueStatsService
{
    public async Task TakeRoundStartSnapshotsAsync(int roundId, CancellationToken cancellationToken)
    {
        await statsRepository.SnapshotRanksForRoundStartAsync(roundId, cancellationToken);
    }

    public async Task UpdateLiveStatsAsync(int roundId, CancellationToken cancellationToken)
    {
        await statsRepository.UpdateLiveStatsAsync(roundId, cancellationToken);
    }

    public async Task UpdateStableStatsAsync(int roundId, CancellationToken cancellationToken)
    {
        await statsRepository.UpdateStableStatsAsync(roundId, cancellationToken);
    }

    public async Task<int> EnsureMemberStatsRowsExistAsync(int roundId, CancellationToken cancellationToken)
    {
        return await statsRepository.EnsureMemberStatsRowsExistAsync(roundId, cancellationToken);
    }
}