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
            // Always at least one member: rankings come from a GroupBy, so no empty-group guard.
            var winnersForThisRank = rankingGroup.Members;

            // A joint group at rank R with N members occupies slots R, R+1, ..., R+N-1.
            // Pool the prize money from every setting whose rank falls within those slots,
            // because the lower-rank finishers that would have claimed them do not exist.
            var firstSlot = rankingGroup.Rank;
            var lastSlot = rankingGroup.Rank + winnersForThisRank.Count - 1;

            var coveredPrizeSettings = overallPrizeSettings
                .Where(p => p.Rank >= firstSlot && p.Rank <= lastSlot)
                .ToList();

            if (coveredPrizeSettings.Count == 0)
                continue;

            var pooledPrizeAmount = coveredPrizeSettings.Sum(p => p.PrizeAmount);
            if (pooledPrizeAmount == 0)
                continue;

            var anchorPrizeSetting = coveredPrizeSettings[0];

            var individualPrizes = PrizeDistributionHelper.DistributePrizeMoney(
                pooledPrizeAmount,
                winnersForThisRank.Count
            );

            for (var i = 0; i < winnersForThisRank.Count; i++)
            {
                var winner = winnersForThisRank[i];
                var prizeAmount = individualPrizes[i];

                var newWinning = Winning.Create(
                    winner.UserId,
                    anchorPrizeSetting.Id,
                    prizeAmount,
                    null,
                    null,
                    dateTimeProvider
                );
                allNewWinnings.Add(newWinning);
            }
        }

        if (allNewWinnings.Any())
            await winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
    }
}