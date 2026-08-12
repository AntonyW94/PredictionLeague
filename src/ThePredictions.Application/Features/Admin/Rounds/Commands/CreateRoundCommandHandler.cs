using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class CreateRoundCommandHandler(
    IRoundRepository roundRepository,
    ISeasonRepository seasonRepository,
    ICurrentUserService currentUserService) : IRequestHandler<CreateRoundCommand, RoundDto>
{
    public async Task<RoundDto> Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
        currentUserService.EnsureAdministrator();

        // A season cannot hold more rounds than it declares: that number divides the prize pot.
        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        var existingRounds = await roundRepository.GetAllForSeasonAsync(request.SeasonId, cancellationToken);
        season.EnsureRoomForAnotherRound(existingRounds.Count);

        var round = Round.Create(
            request.SeasonId,
            request.RoundNumber,
            $"Gameweek {request.RoundNumber}",
            request.StartDateUtc,
            request.DeadlineUtc,
            request.ApiRoundName);

        // Save first: a match is created against its round's id, and an unsaved round has none.
        // Adding matches before this throws "Round ID must be greater than 0".
        var createdRound = await roundRepository.CreateAsync(round, cancellationToken);

        if (request.Matches.Any())
        {
            foreach (var matchToAdd in request.Matches)
            {
                createdRound.AddMatch(matchToAdd.HomeTeamId, matchToAdd.AwayTeamId, matchToAdd.MatchDateTimeUtc, matchToAdd.ExternalId);
            }

            await roundRepository.UpdateAsync(createdRound, cancellationToken);
        }

        return new RoundDto
        (
            createdRound.Id,
            createdRound.SeasonId,
            createdRound.RoundNumber,
            createdRound.ApiRoundName,
            createdRound.StartDateUtc,
            createdRound.DeadlineUtc,
            createdRound.Status,
            createdRound.Matches.Count
        );
    }
}