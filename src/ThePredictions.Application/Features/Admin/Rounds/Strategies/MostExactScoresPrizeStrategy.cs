using ThePredictions.Domain.Services.Prizes;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Strategies;

public class MostExactScoresPrizeStrategy(
    IWinningsRepository winningsRepository,
    IRoundRepository roundRepository,
    ILeagueRepository leagueRepository,
    IDateTimeProvider dateTimeProvider) : IPrizeStrategy
{
    public PrizeType PrizeType => PrizeType.MostExactScores;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var isLastRoundOfSeason = await roundRepository.IsLastRoundOfSeasonAsync(currentRound.Id, currentRound.SeasonId, cancellationToken);
        if (!isLastRoundOfSeason)
            return;

        var league = await leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);

        var exactScoresPrize = league?.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.MostExactScores);
        if (exactScoresPrize == null)
            return;

        if (league != null)
        {
            await winningsRepository.DeleteWinningsForMostExactScoresAsync(league.Id, cancellationToken);

            var exactScoresWinners = league.GetMostExactScoresWinners();
            if (!exactScoresWinners.Any())
                return;

            var individualPrizes = SharedPrizeSplitter.Split(
                exactScoresPrize.PrizeAmount,
                exactScoresWinners.Count
            );

            var allNewWinnings = new List<Winning>();

            for (var i = 0; i < exactScoresWinners.Count; i++)
            {
                var winner = exactScoresWinners[i];
                var prizeAmount = individualPrizes[i];

                var newWinning = Winning.Create(
                    winner.UserId,
                    exactScoresPrize.Id,
                    prizeAmount,
                    null,
                    null,
                    dateTimeProvider
                );
                allNewWinnings.Add(newWinning);
            }

            await winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
        }
    }
}