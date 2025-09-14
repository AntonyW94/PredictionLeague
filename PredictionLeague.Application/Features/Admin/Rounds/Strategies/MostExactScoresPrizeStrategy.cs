using PredictionLeague.Application.Common.Helpers;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Strategies;

public class MostExactScoresPrizeStrategy : IPrizeStrategy
{
    private readonly IWinningsRepository _winningsRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ILeagueRepository _leagueRepository;

    public MostExactScoresPrizeStrategy(
        IWinningsRepository winningsRepository,
        IRoundRepository roundRepository,
        ILeagueRepository leagueRepository)
    {
        _winningsRepository = winningsRepository;
        _roundRepository = roundRepository;
        _leagueRepository = leagueRepository;
    }

    public PrizeType PrizeType => PrizeType.MostExactScores;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await _roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var isLastRoundOfSeason = await _roundRepository.IsLastRoundOfSeasonAsync(currentRound.Id, currentRound.SeasonId, cancellationToken);
        if (!isLastRoundOfSeason)
            return;

        var league = await _leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);

        var exactScoresPrize = league?.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.MostExactScores);
        if (exactScoresPrize == null)
            return;

        if (league != null)
        {
            await _winningsRepository.DeleteWinningsForMostExactScoresAsync(league.Id, cancellationToken);

            var exactScoresWinners = league.GetMostExactScoresWinners();
            if (!exactScoresWinners.Any())
                return;

            var individualPrizes = PrizeDistributionHelper.DistributePrizeMoney(
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
                    null
                );
                allNewWinnings.Add(newWinning);
            }

            await _winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
        }
    }
}