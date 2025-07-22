using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;

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
        Guard.Against.Null(round, $"Round (ID: {request.RoundId}) was not found.");

        round.UpdateDetails(
            request.RoundNumber,
            request.StartDate,
            request.Deadline
        );

        round.ClearMatches();

        foreach (var matchToAdd in request.Matches)
        {
            round.AddMatch(
                matchToAdd.HomeTeamId,
                matchToAdd.AwayTeamId,
                matchToAdd.MatchDateTime
            );
        }

        await _roundRepository.UpdateAsync(round, cancellationToken);
    }
}