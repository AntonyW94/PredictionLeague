using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;

namespace PredictionLeague.Infrastructure.Services;

public class LeagueStatsService : ILeagueStatsService
{
    private readonly ILeagueStatsRepository _statsRepository;

    public LeagueStatsService(ILeagueStatsRepository statsRepository)
    {
        _statsRepository = statsRepository;
    }

    public async Task TakeRoundStartSnapshotsAsync(int roundId, CancellationToken cancellationToken)
    {
        await _statsRepository.SnapshotRanksForRoundStartAsync(roundId, cancellationToken);
    }

    public async Task UpdateLiveStatsAsync(int roundId, CancellationToken cancellationToken)
    {
        await _statsRepository.UpdateLiveStatsAsync(roundId, cancellationToken);
    }

    public async Task UpdateStableStatsAsync(int roundId, CancellationToken cancellationToken)
    {
        await _statsRepository.UpdateStableStatsAsync(roundId, cancellationToken);
    }
}