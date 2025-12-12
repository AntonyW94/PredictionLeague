using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services.Boosts;
using PredictionLeague.Contracts.Boosts;
using PredictionLeague.Domain.Services.Boosts;

namespace PredictionLeague.Infrastructure.Services;

public sealed class BoostService : IBoostService
{
    private readonly IBoostReadRepository _boostReadRepository;
    private readonly IBoostWriteRepository _boostWriteRepository;

    public BoostService(IBoostReadRepository boostReadRepository, IBoostWriteRepository boostWriteRepository)
    {
        _boostReadRepository = boostReadRepository;
        _boostWriteRepository = boostWriteRepository;
    }

    public async Task<BoostEligibilityDto> GetEligibilityAsync(
        string userId,
        int leagueId,
        int roundId,
        string boostCode,
        CancellationToken cancellationToken)
    {
        var (seasonId, roundNumber) = await _boostReadRepository.GetRoundInfoAsync(roundId, cancellationToken);
        var leagueSeasonId = await _boostReadRepository.GetLeagueSeasonIdAsync(leagueId, cancellationToken);
        var isRoundInLeagueSeason = leagueSeasonId == seasonId;
        var isUserMember = await _boostReadRepository.IsUserMemberOfLeagueAsync(userId, leagueId, cancellationToken);
        var ruleSnapshot = await _boostReadRepository.GetLeagueBoostRuleAsync(leagueId, boostCode, cancellationToken);

        if (ruleSnapshot is null)
        {
            return new BoostEligibilityDto
            {
                BoostCode = boostCode,
                LeagueId = leagueId,
                RoundId = roundId,
                CanUse = false,
                Reason = "Boost is not available in this league.",
                RemainingSeasonUses = 0,
                RemainingWindowUses = 0,
                AlreadyUsedThisRound = false
            };
        }

        var usageSnapshot = await _boostReadRepository.GetUserBoostUsageSnapshotAsync(
            userId,
            leagueId,
            seasonId,
            roundId,
            boostCode,
            cancellationToken);

        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: ruleSnapshot.IsEnabled,
            totalUsesPerSeason: ruleSnapshot.TotalUsesPerSeason,
            seasonUses: usageSnapshot.SeasonUses,
            windowUses: usageSnapshot.WindowUses,
            hasUsedThisRound: usageSnapshot.HasUsedThisRound,
            roundNumber: roundNumber,
            windows: ruleSnapshot.Windows,
            isUserMemberOfLeague: isUserMember,
            isRoundInLeagueSeason: isRoundInLeagueSeason);

        return new BoostEligibilityDto
        {
            BoostCode = boostCode,
            LeagueId = leagueId,
            RoundId = roundId,
            CanUse = result.CanUse,
            Reason = result.Reason,
            RemainingSeasonUses = result.RemainingSeasonUses,
            RemainingWindowUses = result.RemainingWindowUses,
            AlreadyUsedThisRound = result.AlreadyUsedThisRound
        };
    }

    public async Task<ApplyBoostResultDto> ApplyBoostAsync(string userId, int leagueId, int roundId, string boostCode, CancellationToken cancellationToken)
    {
        var eligibility = await GetEligibilityAsync(userId, leagueId, roundId, boostCode, cancellationToken);
        if (!eligibility.CanUse)
        {
            return new ApplyBoostResultDto
            {
                Success = false,
                Error = eligibility.Reason ?? "Not eligible to use this boost.",
                AlreadyUsedThisRound = eligibility.AlreadyUsedThisRound
            };
        }

        var (seasonId, _) = await _boostReadRepository.GetRoundInfoAsync(roundId, cancellationToken);

        var (inserted, error) = await _boostWriteRepository.InsertUserBoostUsageAsync(
            userId,
            leagueId,
            seasonId,
            roundId,
            boostCode,
            cancellationToken);

        if (inserted)
        {
            return new ApplyBoostResultDto
            {
                Success = true,
                Error = null,
                AlreadyUsedThisRound = false
            };
        }

        var friendlyError = error switch
        {
            "UnknownBoost" => "Unknown boost type.",
            "NotConfigured" => "This boost is not configured for the selected league.",
            "AlreadyUsedThisRound" => "You have already used a boost for this league and round.",
            "SeasonLimitReached" => "You have reached the season limit for this boost in this league.",
            "WindowLimitReached" => "This boost is not available any more for this round (window limit reached).",
            "NotAvailable" => "This boost is not available for this round.",
            _ => error
        };

        return new ApplyBoostResultDto
        {
            Success = false,
            Error = friendlyError,
            AlreadyUsedThisRound = error == "AlreadyUsedThisRound"
        };
    }

    public async Task<bool> DeleteUserBoostUsageAsync(string userId, int leagueId, int roundId, CancellationToken cancellationToken)
    {
        return await _boostWriteRepository.DeleteUserBoostUsageAsync(userId, leagueId, roundId, cancellationToken);
    }
}