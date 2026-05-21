namespace ThePredictions.Application.Repositories;

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

    Task<int> AutoApplyUnusedBoostsForRoundAsync(int seasonId, int roundId, CancellationToken cancellationToken);
}