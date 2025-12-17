using PredictionLeague.Application.Common.Helpers;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Strategies;

public class MonthlyPrizeStrategy : IPrizeStrategy
{
    private readonly IWinningsRepository _winningsRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ILeagueRepository _leagueRepository;

    public MonthlyPrizeStrategy(
        IWinningsRepository winningsRepository,
        IRoundRepository roundRepository,
        ILeagueRepository leagueRepository)
    {
        _winningsRepository = winningsRepository;
        _roundRepository = roundRepository;
        _leagueRepository = leagueRepository;
    }

    public PrizeType PrizeType => PrizeType.Monthly;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await _roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var league = await _leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);
        if (league == null)
            return;

        var isLastRoundOfMonth = await _roundRepository.IsLastRoundOfMonthAsync(currentRound.Id, currentRound.SeasonId, cancellationToken);
        if (!isLastRoundOfMonth)
            return;

        var monthlyPrize = league.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Monthly);
        if (monthlyPrize == null)
            return;

        var month = currentRound.StartDate.Month;
        await _winningsRepository.DeleteWinningsForMonthAsync(league.Id, month, cancellationToken);

        var roundIdsInMonth = await _roundRepository.GetRoundsIdsForMonthAsync(month, currentRound.SeasonId, cancellationToken);
       
        var monthlyWinners = league.GetPeriodWinners(roundIdsInMonth);
        if (!monthlyWinners.Any())
            return;

        var individualPrizes = PrizeDistributionHelper.DistributePrizeMoney(
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
                month
            );
            allNewWinnings.Add(newWinning);
        }

        await _winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
    }
}