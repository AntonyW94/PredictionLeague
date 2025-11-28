using PredictionLeague.Contracts.Boosts;

namespace PredictionLeague.Application.Services.Boosts;

public interface IBoostService
{
    Task<BoostEligibilityDto> GetBoostEligibilityAsync(
        string userId,
        int leagueId,
        int roundId,
        string boostCode,
        CancellationToken cancellationToken);

    Task<ApplyBoostResultDto> ApplyBoostAsync(
        string userId,
        int leagueId,
        int roundId,
        string boostCode,
        CancellationToken cancellationToken);
}