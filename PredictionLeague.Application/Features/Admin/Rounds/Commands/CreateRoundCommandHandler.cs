using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class CreateRoundCommandHandler : IRequestHandler<CreateRoundCommand, RoundDto>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateRoundCommandHandler(IRoundRepository roundRepository, ICurrentUserService currentUserService)
    {
        _roundRepository = roundRepository;
        _currentUserService = currentUserService;
    }

    public async Task<RoundDto> Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
        _currentUserService.EnsureAdministrator();

        var round = Round.Create(
            request.SeasonId,
            request.RoundNumber,
            request.StartDateUtc,
            request.DeadlineUtc,
            request.ApiRoundName);

        foreach (var matchToAdd in request.Matches)
        {
            round.AddMatch(matchToAdd.HomeTeamId, matchToAdd.AwayTeamId, matchToAdd.MatchDateTimeUtc, matchToAdd.ExternalId);
        }

        var createdRound = await _roundRepository.CreateAsync(round, cancellationToken);

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