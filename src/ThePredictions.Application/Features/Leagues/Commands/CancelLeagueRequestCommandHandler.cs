using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class CancelLeagueRequestCommandHandler(ILeagueMemberRepository leagueMemberRepository) : IRequestHandler<CancelLeagueRequestCommand>
{
    public async Task Handle(CancelLeagueRequestCommand request, CancellationToken cancellationToken)
    {
        var member = await leagueMemberRepository.GetAsync(request.LeagueId, request.UserId, cancellationToken);
        Guard.Against.EntityNotFound(request.UserId, member, "League Join Request");
     
        member.EnsureJoinRequestCanBeCancelled();

        await leagueMemberRepository.DeleteAsync(member, cancellationToken);
    }
}