using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface ILeaguePayoutRepository
{
    Task<LeaguePayout?> GetByLeagueAndUserAsync(int leagueId, string userId, CancellationToken cancellationToken);
    Task UpsertAsync(LeaguePayout payout, CancellationToken cancellationToken);
}
