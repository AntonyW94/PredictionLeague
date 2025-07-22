using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateMatchResultsCommandHandler : IRequestHandler<UpdateMatchResultsCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ILeagueRepository _leagueRepository;

    public UpdateMatchResultsCommandHandler(IRoundRepository roundRepository, ILeagueRepository leagueRepository)
    {
        _roundRepository = roundRepository;
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(UpdateMatchResultsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId);
        Guard.Against.NotFound(request.RoundId, round, $"Round (ID: {request.RoundId}) was not found during Update Match Results.");

        foreach (var matchResult in request.Matches)
        {
            var matchToUpdate = round.Matches.FirstOrDefault(m => m.Id == matchResult.MatchId);
            matchToUpdate?.UpdateScore(matchResult.HomeScore, matchResult.AwayScore, matchResult.Status);
        }

        await _roundRepository.UpdateAsync(round);

        var matchesWithScores = round.Matches
            .Where(m => m.Status != MatchStatus.Scheduled)
            .ToList();

        if (!matchesWithScores.Any())
            return;
        
        var leaguesToScore = (await _leagueRepository.GetLeaguesForScoringAsync(round.SeasonId, round.Id)).ToList();

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

        await _leagueRepository.UpdatePredictionPointsAsync(allUpdatedPredictions);
    }
}