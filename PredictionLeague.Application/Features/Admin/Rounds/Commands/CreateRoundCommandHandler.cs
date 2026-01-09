using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class CreateRoundCommandHandler : IRequestHandler<CreateRoundCommand, RoundDto>
{
    private readonly IRoundRepository _roundRepository;

    public CreateRoundCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<RoundDto> Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
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