using ThePredictions.Application.Common.Helpers;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Strategies;

public class OverallPrizeStrategy(
    IWinningsRepository winningsRepository,
    IRoundRepository roundRepository,
    ILeagueRepository leagueRepository,
    IDateTimeProvider dateTimeProvider) : IPrizeStrategy
{
    public PrizeType PrizeType => PrizeType.Overall;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var isLastRoundOfSeason = await roundRepository.IsLastRoundOfSeasonAsync(currentRound.Id, currentRound.SeasonId, cancellationToken);
        if (!isLastRoundOfSeason)
            return;

        var league = await leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);
        if (league == null)
            return;

        var overallPrizeSettings = league.PrizeSettings
            .Where(p => p.PrizeType == PrizeType.Overall)
            .OrderBy(p => p.Rank)
            .ToList();

        if (!overallPrizeSettings.Any()) return;

        await winningsRepository.DeleteWinningsForOverallAsync(league.Id, cancellationToken);

        var overallRankings = league.GetOverallRankings();
        if (!overallRankings.Any())
            return;

        var allNewWinnings = new List<Winning>();

        foreach (var rankingGroup in overallRankings)
        {
            allNewWinnings.AddRange(WinningsForRank(rankingGroup, overallPrizeSettings));
        }

        if (allNewWinnings.Any())
            await winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
    }

    /// <summary>
    /// A joint group at rank R with N members occupies slots R, R+1, ..., R+N-1. The prize money from
    /// every setting covering those slots is pooled and split evenly, because the lower-rank finishers
    /// that would have claimed them do not exist. Nothing is paid where no setting covers the group,
    /// or where the settings that do are all set to zero.
    /// </summary>
    private List<Winning> WinningsForRank(OverallRanking rankingGroup, List<LeaguePrizeSetting> overallPrizeSettings)
    {
        // Always at least one member: rankings come from a GroupBy, so no empty-group guard.
        var winnersForThisRank = rankingGroup.Members;

        var firstSlot = rankingGroup.Rank;
        var lastSlot = rankingGroup.Rank + winnersForThisRank.Count - 1;

        var coveredPrizeSettings = overallPrizeSettings
            .Where(p => p.Rank >= firstSlot && p.Rank <= lastSlot)
            .ToList();

        if (coveredPrizeSettings.Count == 0)
            return [];

        var pooledPrizeAmount = coveredPrizeSettings.Sum(p => p.PrizeAmount);
        if (pooledPrizeAmount == 0)
            return [];

        var anchorPrizeSetting = coveredPrizeSettings[0];
        var individualPrizes = PrizeDistributionHelper.DistributePrizeMoney(pooledPrizeAmount, winnersForThisRank.Count);

        return winnersForThisRank
            .Select((winner, i) => Winning.Create(
                winner.UserId,
                anchorPrizeSetting.Id,
                individualPrizes[i],
                null,
                null,
                dateTimeProvider))
            .ToList();
    }
}