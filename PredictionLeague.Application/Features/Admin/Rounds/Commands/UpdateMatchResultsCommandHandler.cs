﻿using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common.Guards.Season;

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
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        Guard.Against.EntityNotFound(request.RoundId, round, "Round");

        foreach (var matchResult in request.Matches)
        {
            var matchToUpdate = round.Matches.FirstOrDefault(m => m.Id == matchResult.MatchId);
            matchToUpdate?.UpdateScore(matchResult.HomeScore, matchResult.AwayScore, matchResult.Status);
        }

        await _roundRepository.UpdateAsync(round, cancellationToken);

        var matchesWithScores = round.Matches
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
    }
}