using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services.Boosts;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common.Guards.Season;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateMatchResultsCommandHandler : IRequestHandler<UpdateMatchResultsCommand>
{
    private readonly IMediator _mediator;
    private readonly IBoostService _boostService;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IUserPredictionRepository _userPredictionRepository;

    public UpdateMatchResultsCommandHandler(IMediator mediator, IBoostService boostService, ILeagueRepository leagueRepository, IRoundRepository roundRepository, IUserPredictionRepository userPredictionRepository)
    {
        _mediator = mediator;
        _boostService = boostService;
        _leagueRepository = leagueRepository;
        _roundRepository = roundRepository;
        _userPredictionRepository = userPredictionRepository;
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

        if (!matchesToUpdate.Any())
            return;

        await _roundRepository.UpdateMatchScoresAsync(matchesToUpdate, cancellationToken);

        var matchIds = matchesToUpdate.Select(m => m.Id).ToList();
        var predictionsToUpdate = (await _userPredictionRepository.GetByMatchIdsAsync(matchIds, cancellationToken)).ToList();

        foreach (var prediction in predictionsToUpdate)
        {
            var match = matchesToUpdate.FirstOrDefault(m => m.Id == prediction.MatchId);
            if (match == null)
                continue;
            
            prediction.SetOutcome(match.Status, match.ActualHomeTeamScore, match.ActualAwayTeamScore);
        }

        await _userPredictionRepository.UpdateOutcomesAsync(predictionsToUpdate, cancellationToken);
        await _roundRepository.UpdateRoundResultsAsync(round.Id, cancellationToken);
        await _leagueRepository.UpdateLeagueRoundResultsAsync(round.Id, cancellationToken);
        await _boostService.ApplyRoundBoostsAsync(round.Id, cancellationToken);

        if (round.Matches.All(m => m.Status == MatchStatus.Completed))
        {
            round.UpdateStatus(RoundStatus.Completed);
            await _roundRepository.UpdateAsync(round, cancellationToken);

            var leagueIds = await _leagueRepository.GetLeagueIdsForSeasonAsync(round.SeasonId, cancellationToken);

            foreach (var leagueId in leagueIds)
            {
                var processPrizesCommand = new ProcessPrizesCommand
                {
                    RoundId = round.Id,
                    LeagueId = leagueId
                };
                await _mediator.Send(processPrizesCommand, cancellationToken);
            }
        }
    }
}