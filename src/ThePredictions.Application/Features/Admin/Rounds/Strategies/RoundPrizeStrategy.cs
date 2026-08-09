using ThePredictions.Domain.Services.Prizes;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Strategies;

public class RoundPrizeStrategy(
    IWinningsRepository winningsRepository,
    IRoundRepository roundRepository,
    ILeagueRepository leagueRepository,
    IDateTimeProvider dateTimeProvider) : IPrizeStrategy
{
    public PrizeType PrizeType => PrizeType.Round;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var round = await roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (round == null)
            return;
        
        var league = await leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);

        var roundPrize = league?.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Round);
        if (roundPrize == null)
            return;

        if (league != null)
        {
            await winningsRepository.DeleteWinningsForRoundAsync(league.Id, round.RoundNumber, cancellationToken);

            var roundWinners = league.GetRoundWinners(round.Id);
            if (!roundWinners.Any())
                return;

            var individualPrizes = SharedPrizeSplitter.Split(
                roundPrize.PrizeAmount,
                roundWinners.Count
            );

            var allNewWinnings = new List<Winning>();

            for (var i = 0; i < roundWinners.Count; i++)
            {
                var winner = roundWinners[i];
                var prizeAmount = individualPrizes[i];

                var newWinning = Winning.Create(
                    winner.UserId,
                    roundPrize.Id,
                    prizeAmount,
                    round.RoundNumber,
                    null,
                    dateTimeProvider
                );
                allNewWinnings.Add(newWinning);
            }


            await winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
        }
    }
}