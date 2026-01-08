using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class CancelLeagueRequestCommandHandler : IRequestHandler<CancelLeagueRequestCommand>
{
    private readonly ILeagueMemberRepository _leagueMemberRepository;

    public CancelLeagueRequestCommandHandler(ILeagueMemberRepository leagueMemberRepository)
    {
        _leagueMemberRepository = leagueMemberRepository;
    }

    public async Task Handle(CancelLeagueRequestCommand request, CancellationToken cancellationToken)
    {
        var member = await _leagueMemberRepository.GetAsync(request.LeagueId, request.UserId, cancellationToken);
        Guard.Against.EntityNotFound(request.UserId, member, "League Join Request");
     
        if (member.Status != LeagueMemberStatus.Pending)
            throw new InvalidOperationException("You can only cancel requests that are currently pending.");

        await _leagueMemberRepository.DeleteAsync(member, cancellationToken);
    }
}