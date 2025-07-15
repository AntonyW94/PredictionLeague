using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Transactions;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class CreateRoundCommandHandler : IRequestHandler<CreateRoundCommand>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IMatchRepository _matchRepository;

    public CreateRoundCommandHandler(IRoundRepository roundRepository, IMatchRepository matchRepository)
    {
        _roundRepository = roundRepository;
        _matchRepository = matchRepository;
    }

    public async Task Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var round = new Round
        {
            SeasonId = request.SeasonId,
            RoundNumber = request.RoundNumber,
            StartDate = request.StartDate,
            Deadline = request.Deadline
        };

        var createdRound = await _roundRepository.AddAsync(round);

        foreach (var matchRequest in request.Matches)
        {
            var match = new Match
            {
                RoundId = createdRound.Id,
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