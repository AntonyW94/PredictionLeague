using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Matches;
using PredictionLeague.Shared.Admin.Rounds;
using System.Transactions;

namespace PredictionLeague.Infrastructure.Services;

public class RoundService : IRoundService
{
    private readonly IRoundRepository _roundRepository;
    private readonly IRoundResultRepository _roundResultRepository;
    private readonly IPredictionService _predictionService;
    private readonly IUserPredictionRepository _predictionRepository;
    private readonly IMatchRepository _matchRepository;

    public RoundService(
        IRoundResultRepository roundResultRepository,
        IPredictionService predictionService,
        IUserPredictionRepository predictionRepository,
        IMatchRepository matchRepository, IRoundRepository roundRepository)
    {
        _roundResultRepository = roundResultRepository;
        _predictionService = predictionService;
        _predictionRepository = predictionRepository;
        _matchRepository = matchRepository;
        _roundRepository = roundRepository;
    }

    #region Create

    public async Task CreateAsync(CreateRoundRequest request)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var round = new Round { SeasonId = request.SeasonId, RoundNumber = request.RoundNumber, StartDate = request.StartDate, Deadline = request.Deadline };
        var createdRound = await _roundRepository.AddAsync(round);

        foreach (var matchRequest in request.Matches)
        {
            var match = new Match
            {
                RoundId = createdRound.Id,
                HomeTeamId = matchRequest.HomeTeamId,
                AwayTeamId = matchRequest.AwayTeamId,
                MatchDateTime = matchRequest.MatchDateTime,
                Status = MatchStatus.Scheduled
            };

            await _matchRepository.AddAsync(match);
        }

        scope.Complete();
    }

    #endregion

    #region Read

    public async Task<RoundDetailsDto?> GetByIdAsync(int roundId)
    {
        var round = await _roundRepository.GetByIdAsync(roundId);
        if (round == null)
            return null;

        var matches = await _matchRepository.GetByRoundIdAsync(roundId);

        var response = new RoundDetailsDto
        {
            Round = new RoundDto
            {
                Id = round.Id,
                SeasonId = round.SeasonId,
                RoundNumber = round.RoundNumber,
                StartDate = round.StartDate,
                Deadline = round.Deadline
            },
            Matches = matches.Select(m => new MatchDto
            {
                Id = m.Id,
                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam?.Name ?? "N/A",
                HomeTeamLogoUrl = m.HomeTeam?.LogoUrl ?? "",
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam?.Name ?? "N/A",
                AwayTeamLogoUrl = m.AwayTeam?.LogoUrl ?? "",
                MatchDateTime = m.MatchDateTime,
                ActualHomeScore = m.ActualHomeTeamScore,
                ActualAwayScore = m.ActualAwayTeamScore
            }).ToList()
        };

        return response;
    }

    public async Task<IEnumerable<RoundDto>> FetchBySeasonIdAsync(int seasonId)
    {
        var rounds = await _roundRepository.GetBySeasonIdAsync(seasonId);
        var roundsToReturn = new List<RoundDto>();

        foreach (var round in rounds)
        {
            var matches = await _matchRepository.GetByRoundIdAsync(round.Id);
            roundsToReturn.Add(new RoundDto
            {
                Id = round.Id,
                RoundNumber = round.RoundNumber,
                StartDate = round.StartDate,
                Deadline = round.Deadline,
                MatchCount = matches.Count()
            });
        }

        return roundsToReturn;
    }

    #endregion

    #region Update

    public async Task UpdateAsync(int roundId, UpdateRoundRequest request)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var round = await _roundRepository.GetByIdAsync(roundId) ?? throw new KeyNotFoundException("Round not found.");
        round.StartDate = request.StartDate;
        round.Deadline = request.Deadline;

        await _roundRepository.UpdateAsync(round);
        await _matchRepository.DeleteByRoundIdAsync(roundId);

        foreach (var match in request.Matches.Select(matchRequest => new Match { RoundId = roundId, HomeTeamId = matchRequest.HomeTeamId, AwayTeamId = matchRequest.AwayTeamId, MatchDateTime = matchRequest.MatchDateTime }))
        {
            await _matchRepository.AddAsync(match);
        }

        scope.Complete();
    }

    public async Task UpdateResultsAsync(int roundId, IEnumerable<Match> completedMatches)
    {
        // First, calculate points for each match individually
        foreach (var match in completedMatches)
        {
            if (match.Status == MatchStatus.Completed)
            {
                await _predictionService.CalculatePointsForMatchAsync(match.Id);
            }
        }

        // Now, aggregate the results for the entire round
        var allPredictionsForRound = new List<UserPrediction>();
        var matchesInRound = await _matchRepository.GetByRoundIdAsync(roundId);
        foreach (var match in matchesInRound)
        {
            allPredictionsForRound.AddRange(await _predictionRepository.GetByMatchIdAsync(match.Id));
        }

        var userScores = allPredictionsForRound
            .GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(p => p.PointsAwarded ?? 0)
            });

        foreach (var score in userScores)
        {
            var roundResult = new RoundResult
            {
                RoundId = roundId,
                UserId = score.UserId,
                TotalPoints = score.TotalPoints
            };
            await _roundResultRepository.UpsertAsync(roundResult);
        }
    }

    #endregion
}