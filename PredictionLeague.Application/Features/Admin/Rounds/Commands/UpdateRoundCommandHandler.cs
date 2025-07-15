using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Transactions;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class UpdateRoundCommandHandler : IRequestHandler<UpdateRoundCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IMatchRepository _matchRepository;

    public UpdateRoundCommandHandler(IRoundRepository roundRepository, IMatchRepository matchRepository)
    {
        _roundRepository = roundRepository;
        _matchRepository = matchRepository;
    }

    public async Task Handle(UpdateRoundCommand request, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var round = await _roundRepository.GetByIdAsync(request.RoundId) ?? throw new KeyNotFoundException($"Round with ID {request.RoundId} was not found.");

        round.StartDate = request.StartDate;
        round.Deadline = request.Deadline;
        await _roundRepository.UpdateAsync(round);

        await _matchRepository.DeleteByRoundIdAsync(request.RoundId);

        foreach (var matchRequest in request.Matches)
        {
            var match = new Match
            {
                RoundId = request.RoundId,
                HomeTeamId = matchRequest.HomeTeamId,
                AwayTeamId = matchRequest.AwayTeamId,
                MatchDateTime = matchRequest.MatchDateTime,
                Status = MatchStatus.Scheduled
            };
            
            await _matchRepository.AddAsync(match);
        }

        scope.Complete();
    }
}