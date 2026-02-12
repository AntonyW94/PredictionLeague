using PredictionLeague.Application.Common.Helpers;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Strategies;

public class OverallPrizeStrategy : IPrizeStrategy
{
    private readonly IWinningsRepository _winningsRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public OverallPrizeStrategy(
        IWinningsRepository winningsRepository,
        IRoundRepository roundRepository,
        ILeagueRepository leagueRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _winningsRepository = winningsRepository;
        _roundRepository = roundRepository;
        _leagueRepository = leagueRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public PrizeType PrizeType => PrizeType.Overall;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await _roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var isLastRoundOfSeason = await _roundRepository.IsLastRoundOfSeasonAsync(currentRound.Id, currentRound.SeasonId, cancellationToken);
        if (!isLastRoundOfSeason)
            return;

        var league = await _leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);
        if (league == null)
            return;

        var overallPrizeSettings = league.PrizeSettings
            .Where(p => p.PrizeType == PrizeType.Overall)
            .OrderBy(p => p.Rank)
            .ToList();

        if (!overallPrizeSettings.Any()) return;

        await _winningsRepository.DeleteWinningsForOverallAsync(league.Id, cancellationToken);

        var overallRankings = league.GetOverallRankings();
        if (!overallRankings.Any())
            return;

        var allNewWinnings = new List<Winning>();

        foreach (var prizeSetting in overallPrizeSettings)
        {
            var rankingGroup = overallRankings.FirstOrDefault(r => r.Rank == prizeSetting.Rank);
            if (rankingGroup == null || !rankingGroup.Members.Any())
                continue;
            
            var winnersForThisRank = rankingGroup.Members;

            var individualPrizes = PrizeDistributionHelper.DistributePrizeMoney(
                prizeSetting.PrizeAmount,
                winnersForThisRank.Count
            );

            for (var i = 0; i < winnersForThisRank.Count; i++)
            {
                var winner = winnersForThisRank[i];
                var prizeAmount = individualPrizes[i]; 

                var newWinning = Winning.Create(
                    winner.UserId,
                    prizeSetting.Id,
                    prizeAmount,
                    null,
                    null,
                    _dateTimeProvider
                );
                allNewWinnings.Add(newWinning);
            }
        }

        if (allNewWinnings.Any())
            await _winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
    }
}