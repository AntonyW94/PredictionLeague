using ThePredictions.Domain.Services.Prizes;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Strategies;

public class MonthlyPrizeStrategy(
    IWinningsRepository winningsRepository,
    IRoundRepository roundRepository,
    ILeagueRepository leagueRepository,
    IDateTimeProvider dateTimeProvider) : IPrizeStrategy
{
    public PrizeType PrizeType => PrizeType.Monthly;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var league = await leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);
        if (league == null)
            return;

        var isLastRoundOfMonth = await roundRepository.IsLastRoundOfMonthAsync(currentRound.Id, currentRound.SeasonId, cancellationToken);
        if (!isLastRoundOfMonth)
            return;

        var monthlyPrize = league.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Monthly);
        if (monthlyPrize == null)
            return;

        var month = currentRound.StartDateUtc.Month;
        await winningsRepository.DeleteWinningsForMonthAsync(league.Id, month, cancellationToken);

        var roundIdsInMonth = await roundRepository.GetRoundsIdsForMonthAsync(month, currentRound.SeasonId, cancellationToken);
       
        var monthlyWinners = league.GetPeriodWinners(roundIdsInMonth);
        if (!monthlyWinners.Any())
            return;

        var individualPrizes = SharedPrizeSplitter.Split(
            monthlyPrize.PrizeAmount,
            monthlyWinners.Count
        );

        var allNewWinnings = new List<Winning>();

        for (var i = 0; i < monthlyWinners.Count; i++)
        {
            var winner = monthlyWinners[i];
            var prizeAmount = individualPrizes[i];

            var newWinning = Winning.Create(
                winner.UserId,
                monthlyPrize.Id,
                prizeAmount,
                null,
                month,
                dateTimeProvider
            );
            allNewWinnings.Add(newWinning);
        }

        await winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
    }
}