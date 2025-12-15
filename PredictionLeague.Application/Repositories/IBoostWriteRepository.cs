using PredictionLeague.Contracts.Boosts;

namespace PredictionLeague.Application.Repositories;

public interface IBoostWriteRepository
{
    Task<(bool Inserted, string? Error)> InsertUserBoostUsageAsync(
        string userId,
        int leagueId,
        int seasonId,
        int roundId,
        string boostCode,
        CancellationToken cancellationToken);

    Task<bool> DeleteUserBoostUsageAsync(string userId, int leagueId, int roundId, CancellationToken cancellationToken);
}