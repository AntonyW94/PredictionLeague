using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateMatchResultsCommandHandler : IRequestHandler<UpdateMatchResultsCommand>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IWinningsRepository _winningsRepository;

    public UpdateMatchResultsCommandHandler(ILeagueRepository leagueRepository, IRoundRepository roundRepository, ISeasonRepository seasonRepository, IWinningsRepository winningsRepository)
    {
        _leagueRepository = leagueRepository;
        _roundRepository = roundRepository;
        _seasonRepository = seasonRepository;
        _winningsRepository = winningsRepository;
    }

    public async Task Handle(UpdateMatchResultsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        Guard.Against.EntityNotFound(request.RoundId, round, "Round");

        var matchesToUpdate = new List<Match>();

        foreach (var matchResult in request.Matches)
        {
            var matchToUpdate = round.Matches.FirstOrDefault(m => m.Id == matchResult.MatchId);
            if (matchToUpdate != null)
            {
                matchToUpdate.UpdateScore(matchResult.HomeScore, matchResult.AwayScore, matchResult.Status);
                matchesToUpdate.Add(matchToUpdate);
            }
        }

        if (matchesToUpdate.Any())
            await _roundRepository.UpdateMatchScoresAsync(matchesToUpdate, cancellationToken);

        var matchesWithScores = matchesToUpdate
            .Where(m => m.Status != MatchStatus.Scheduled && m.ActualHomeTeamScore.HasValue && m.ActualAwayTeamScore.HasValue)
            .ToList();

        if (!matchesWithScores.Any())
            return;

        var leaguesToScore = (await _leagueRepository.GetLeaguesForScoringAsync(round.SeasonId, round.Id, cancellationToken)).ToList();

        foreach (var league in leaguesToScore)
        {
            foreach (var scoredMatch in matchesWithScores)
            {
                league.ScoreMatch(scoredMatch);
            }
        }

        var allUpdatedPredictions = leaguesToScore
            .SelectMany(l => l.Members)
            .SelectMany(m => m.Predictions);

        await _leagueRepository.UpdatePredictionPointsAsync(allUpdatedPredictions, cancellationToken);

        if (round.Matches.All(m => m.Status == MatchStatus.Completed))
        {
            round.UpdateStatus(RoundStatus.Completed);
            await _roundRepository.UpdateAsync(round, cancellationToken);
            
            await ProcessPrizesAsync(round, leaguesToScore, cancellationToken);
        }
    }

    private async Task ProcessPrizesAsync(Round round, List<League> leagues, CancellationToken cancellationToken)
    {
        var allNewWinnings = new List<Winning>();

        foreach (var league in leagues)
        {
            var roundPrize = league.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Round);
            if (roundPrize != null)
            {
                await _winningsRepository.DeleteWinningsForRoundAsync(league.Id, round.RoundNumber, cancellationToken);

                var roundWinners = league.GetTopScorersForRound(round.Id, round.Matches);
                if (roundWinners.Any())
                {
                    var individualPrizes = DistributePrizeMoney(roundPrize.PrizeAmount, roundWinners.Count);
                    for (var i = 0; i < roundWinners.Count; i++)
                    {
                        var winner = roundWinners[i];
                        var prizeAmount = individualPrizes[i];
                        var newWinning = Winning.Create(winner.UserId, roundPrize.Id, prizeAmount, round.RoundNumber, null);
                        allNewWinnings.Add(newWinning);
                    }
                }
            }

            var isLastRoundOfMonth = await _roundRepository.IsLastRoundOfMonthAsync(round.Id, round.SeasonId, cancellationToken);
            if (isLastRoundOfMonth)
            {
                var monthlyPrize = league.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Monthly);
                if (monthlyPrize != null)
                {
                    var month = round.StartDate.Month;

                    await _winningsRepository.DeleteWinningsForMonthAsync(league.Id, month, cancellationToken);

                    var allMatchesInMonth = (await _roundRepository.GetAllMatchesForMonthAsync(month, round.SeasonId, cancellationToken)).ToList();

                    var monthlyWinners = league.GetTopScorersForMonth(month, allMatchesInMonth);
                    if (monthlyWinners.Any())
                    {
                        var individualPrizes = DistributePrizeMoney(monthlyPrize.PrizeAmount, monthlyWinners.Count);
                        for (var i = 0; i < monthlyWinners.Count; i++)
                        {
                            var winner = monthlyWinners[i];
                            var prizeAmount = individualPrizes[i];
                            var newWinning = Winning.Create(winner.UserId, monthlyPrize.Id, prizeAmount, null, month);
                            allNewWinnings.Add(newWinning);
                        }
                    }
                }
            }

            var totalRoundsInSeason = await _seasonRepository.GetRoundCountAsync(round.SeasonId, cancellationToken);
            if (round.RoundNumber == totalRoundsInSeason)
            {
                await _winningsRepository.DeleteWinningsForEndOfSeasonAsync(league.Id, cancellationToken);

                var overallPrizes = league.PrizeSettings
                   .Where(p => p.PrizeType == PrizeType.Overall)
                   .OrderBy(p => p.Rank) 
                   .ToList();

                if (overallPrizes.Any())
                {
                    var rankings = league.GetOverallRankings();
                    var prizeIndex = 0;

                    foreach (var ranking in rankings)
                    {
                        if (prizeIndex >= overallPrizes.Count)
                            break;

                        var prizesToSplit = overallPrizes.Skip(prizeIndex).Take(ranking.Members.Count).ToList();
                        if (prizesToSplit.Any())
                        {
                            var totalPrizePool = prizesToSplit.Sum(p => p.PrizeAmount);
                            var individualPrizes = DistributePrizeMoney(totalPrizePool, ranking.Members.Count);

                            for (var i = 0; i < ranking.Members.Count; i++)
                            {
                                var member = ranking.Members[i];
                                var individualPrize = individualPrizes[i];
                                var prizeSetting = overallPrizes[prizeIndex];
                                var newWinning = Winning.Create(member.UserId, prizeSetting.Id, individualPrize, null, null);
                                allNewWinnings.Add(newWinning);
                            }
                        }
                        prizeIndex += ranking.Members.Count;
                    }
                }

                var mostExactScoresPrize = league.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.MostExactScores);
                if (mostExactScoresPrize != null)
                {
                    var winners = league.GetMostExactScoresWinners();
                    if (winners.Any())
                    {
                        var individualPrizes = DistributePrizeMoney(mostExactScoresPrize.PrizeAmount, winners.Count);
                        for (var i = 0; i < winners.Count; i++)
                        {
                            var winner = winners[i];
                            var prizeAmount = individualPrizes[i];
                            var newWinning = Winning.Create(winner.UserId, mostExactScoresPrize.Id, prizeAmount, null, null);
                            allNewWinnings.Add(newWinning);
                        }
                    }
                }
            }

            if (allNewWinnings.Any())
                await _winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
        }
    }

    private List<decimal> DistributePrizeMoney(decimal totalAmount, int winnerCount)
    {
        if (winnerCount == 0)
            return new List<decimal>();

        var totalPennies = (int)(totalAmount * 100);
        var basePennies = totalPennies / winnerCount;
        var remainderPennies = totalPennies % winnerCount;

        var amounts = new List<decimal>();
     
        for (var i = 0; i < winnerCount; i++)
        {
            var pennies = basePennies;
            if (remainderPennies > 0)
            {
                pennies++;
                remainderPennies--;
            }
            amounts.Add((decimal)pennies / 100);
        }
        return amounts;
    }
}