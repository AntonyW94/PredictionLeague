using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services.Boosts;

namespace ThePredictions.Infrastructure.Services;

public sealed class BoostService(IBoostReadRepository boostReadRepository, IBoostWriteRepository boostWriteRepository, ILeagueRepository leagueRepository, IDateTimeProvider dateTimeProvider) : IBoostService
{
    public async Task<BoostEligibilityDto> GetEligibilityAsync(
        string userId,
        int leagueId,
        int roundId,
        string boostCode,
        CancellationToken cancellationToken)
    {
        var (seasonId, roundNumber, deadlineUtc) = await boostReadRepository.GetRoundInfoAsync(roundId, cancellationToken);

        // Check deadline first - cannot apply boost after round deadline has passed
        if (deadlineUtc < dateTimeProvider.UtcNow)
        {
            return new BoostEligibilityDto
            {
                BoostCode = boostCode,
                LeagueId = leagueId,
                RoundId = roundId,
                CanUse = false,
                Reason = "Cannot apply boost after round deadline has passed.",
                RemainingSeasonUses = 0,
                RemainingWindowUses = 0,
                AlreadyUsedThisRound = false
            };
        }

        var leagueSeasonId = await boostReadRepository.GetLeagueSeasonIdAsync(leagueId, cancellationToken);
        var isRoundInLeagueSeason = leagueSeasonId == seasonId;
        var isUserMember = await boostReadRepository.IsUserMemberOfLeagueAsync(userId, leagueId, cancellationToken);
        var ruleSnapshot = await boostReadRepository.GetLeagueBoostRuleAsync(leagueId, boostCode, cancellationToken);

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

        var usageSnapshot = await boostReadRepository.GetUserBoostUsageSnapshotAsync(
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

        var (isRoundInActiveWindow, nextWindowStartRound) =
            ComputeWindowStatus(roundNumber, ruleSnapshot.Windows);

        return new BoostEligibilityDto
        {
            BoostCode = boostCode,
            LeagueId = leagueId,
            RoundId = roundId,
            CanUse = result.CanUse,
            Reason = result.Reason,
            RemainingSeasonUses = result.RemainingSeasonUses,
            RemainingWindowUses = result.RemainingWindowUses,
            AlreadyUsedThisRound = result.AlreadyUsedThisRound,
            IsRoundInActiveWindow = isRoundInActiveWindow,
            NextWindowStartRound = nextWindowStartRound
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

        var (seasonId, _, _) = await boostReadRepository.GetRoundInfoAsync(roundId, cancellationToken);

        var (inserted, error) = await boostWriteRepository.InsertUserBoostUsageAsync(
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
        return await boostWriteRepository.DeleteUserBoostUsageAsync(userId, leagueId, roundId, cancellationToken);
    }

    private static (bool IsRoundInActiveWindow, int? NextWindowStartRound) ComputeWindowStatus(
        int roundNumber, IReadOnlyList<BoostWindowSnapshot>? windows)
    {
        if (windows == null || windows.Count == 0)
            return (true, null);

        var isInWindow = windows.Any(w =>
            roundNumber >= w.StartRoundNumber && roundNumber <= w.EndRoundNumber);

        if (isInWindow)
            return (true, null);

        var nextStart = windows
            .Where(w => roundNumber < w.StartRoundNumber)
            .OrderBy(w => w.StartRoundNumber)
            .Select(w => (int?)w.StartRoundNumber)
            .FirstOrDefault();

        return (false, nextStart);
    }

    public async Task ApplyRoundBoostsAsync(int roundId, CancellationToken cancellationToken)
    {
        var leagueResults = (await leagueRepository.GetLeagueRoundResultsAsync(roundId, cancellationToken)).ToList();

        if (!leagueResults.Any())
            return;

        var boosts = await boostReadRepository.GetBoostsForRoundAsync(roundId, cancellationToken);

        var boostLookup = boosts.ToDictionary(
            b => (b.LeagueId, b.UserId),
            b => b.BoostCode);

        var updates = new List<LeagueRoundBoostUpdate>();

        foreach (var result in leagueResults)
        {
            if (!boostLookup.TryGetValue((result.LeagueId, result.UserId), out var boostCode))
                continue;

            result.ApplyBoost(boostCode);

            updates.Add(new LeagueRoundBoostUpdate(
                result.LeagueId,
                result.RoundId,
                result.UserId,
                result.BoostedPoints,
                true,
                boostCode
            ));
        }

        if (updates.Any())
            await leagueRepository.UpdateLeagueRoundBoostsAsync(updates, cancellationToken);
    }

    public async Task<int> AutoApplyUnusedBoostsForLastRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        var (seasonId, _, _) = await boostReadRepository.GetRoundInfoAsync(roundId, cancellationToken);

        return await boostWriteRepository.AutoApplyUnusedBoostsForRoundAsync(seasonId, roundId, cancellationToken);
    }
}