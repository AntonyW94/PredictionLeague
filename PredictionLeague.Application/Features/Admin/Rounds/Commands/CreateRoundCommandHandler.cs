using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class CreateRoundCommandHandler : IRequestHandler<CreateRoundCommand>
{
    private readonly IRoundRepository _roundRepository;

    public CreateRoundCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
        var round = Round.Create(
            request.SeasonId,
            request.RoundNumber,
            request.StartDate,
            request.Deadline);

        foreach (var matchToAdd in request.Matches)
        {
            round.AddMatch(matchToAdd.HomeTeamId, matchToAdd.AwayTeamId, matchToAdd.MatchDateTime);
        }

        await _roundRepository.CreateAsync(round);
    }
}