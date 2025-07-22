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
            request.StartDate,
            request.Deadline);

        foreach (var matchToAdd in request.Matches)
        {
            round.AddMatch(matchToAdd.HomeTeamId, matchToAdd.AwayTeamId, matchToAdd.MatchDateTime);
        }

        var createdRound = await _roundRepository.CreateAsync(round);
        
        return new RoundDto
        {
            Id = createdRound.Id,
            SeasonId = createdRound.SeasonId,
            RoundNumber = createdRound.RoundNumber,
            StartDate = createdRound.StartDate,
            Deadline = createdRound.Deadline,
            MatchCount = createdRound.Matches.Count
        };
    }
}