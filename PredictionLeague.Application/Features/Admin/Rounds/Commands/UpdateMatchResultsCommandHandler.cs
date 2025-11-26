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
    private readonly IMediator _mediator;
    
    public UpdateMatchResultsCommandHandler(ILeagueRepository leagueRepository, IRoundRepository roundRepository, IMediator mediator)
    {
        _leagueRepository = leagueRepository;
        _roundRepository = roundRepository;
        _mediator = mediator;
    }

    public async Task Handle(UpdateMatchResultsCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        Guard.Against.EntityNotFound(request.RoundId, round, "Round");

        var matchesToUpdate = new List<Match>();

        foreach (var matchResult in request.Matches)
        {
            var matchToUpdate = round.Matches.FirstOrDefault(m => m.Id == matchResult.MatchId);
            if (matchToUpdate == null)
                continue;
            
            matchToUpdate.UpdateScore(matchResult.HomeScore, matchResult.AwayScore, matchResult.Status);
            matchesToUpdate.Add(matchToUpdate);
        }

        if (matchesToUpdate.Any())
            await _roundRepository.UpdateMatchScoresAsync(matchesToUpdate, cancellationToken);
        else
            return;

        var leaguesToScore = (await _leagueRepository.GetLeaguesForScoringAsync(round.SeasonId, round.Id, cancellationToken)).ToList();

        foreach (var league in leaguesToScore)
        {
            foreach (var scoredMatch in matchesToUpdate)
            {
                league.ScoreMatch(scoredMatch);
            }
        }

        var matchIdsToUpdate = matchesToUpdate.Select(ma => ma.Id).ToHashSet();

        var allUpdatedPredictions = leaguesToScore
            .SelectMany(l => l.Members)
            .SelectMany(m => m.Predictions)
            .Where(p => matchIdsToUpdate.Contains(p.MatchId))
            .DistinctBy(u => u.Id);

        await _leagueRepository.UpdatePredictionPointsAsync(allUpdatedPredictions, cancellationToken);
        await _roundRepository.UpdateRoundResultsAsync(round.Id, cancellationToken);
        await _leagueRepository.UpdateLeagueRoundResultsAsync(round.Id, cancellationToken);
        // await _boostService.ApplyRoundBoostsAsync(round.Id, cancellationToken);

        if (round.Matches.All(m => m.Status == MatchStatus.Completed))
        {
            round.UpdateStatus(RoundStatus.Completed);
            await _roundRepository.UpdateAsync(round, cancellationToken);
           
            foreach (var league in leaguesToScore)
            {
                var processPrizesCommand = new ProcessPrizesCommand
                {
                    RoundId = round.Id,
                    LeagueId = league.Id
                };
                await _mediator.Send(processPrizesCommand, cancellationToken);
            }
        }
    }
}