using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateRoundCommandHandler : IRequestHandler<UpdateRoundCommand>
{
    private readonly IRoundRepository _roundRepository;

    public UpdateRoundCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task Handle(UpdateRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        Guard.Against.EntityNotFound(request.RoundId, round, "Round");

        round.UpdateDetails(
            request.RoundNumber,
            request.StartDateUtc,
            request.DeadlineUtc,
            request.Status,
            request.ApiRoundName
        );
        
        var existingMatches = round.Matches.ToDictionary(m => m.Id);
        var incomingMatchIds = request.Matches.Select(m => m.Id).ToHashSet();

        foreach (var matchDto in request.Matches)
        {
            switch (matchDto.Id)
            {
                case > 0 when existingMatches.TryGetValue(matchDto.Id, out var existingMatch):
                    existingMatch.UpdateDetails(matchDto.HomeTeamId, matchDto.AwayTeamId, matchDto.MatchDateTimeUtc);
                    break;
              
                case 0:
                    round.AddMatch(matchDto.HomeTeamId, matchDto.AwayTeamId, matchDto.MatchDateTimeUtc, matchDto.ExternalId);
                    break;
            }
        }

        var matchesToDelete = existingMatches.Values.Where(m => !incomingMatchIds.Contains(m.Id)).ToList();
        if (matchesToDelete.Any())
        {
            var matchIdsToDelete = matchesToDelete.Select(m => m.Id).ToList();
          
            var matchesWithPredictions = await _roundRepository.GetMatchIdsWithPredictionsAsync(matchIdsToDelete, cancellationToken);
            if (matchesWithPredictions.Any())
                throw new InvalidOperationException("Cannot delete a match that already has user predictions.");
            
            foreach (var matchToRemove in matchesToDelete)
            {
                round.RemoveMatch(matchToRemove.Id);
            }
        }

        await _roundRepository.UpdateAsync(round, cancellationToken);
    }
}