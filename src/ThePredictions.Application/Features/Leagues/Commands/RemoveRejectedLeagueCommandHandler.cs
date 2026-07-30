using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class RemoveRejectedLeagueCommandHandler(ILeagueRepository leagueRepository) : IRequestHandler<RemoveRejectedLeagueCommand>
{
    public async Task Handle(RemoveRejectedLeagueCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        var member = league.Members.FirstOrDefault(m => m.UserId == request.CurrentUserId);
        if (member is null || member.Status != LeagueMemberStatus.Rejected)
            throw new BusinessRuleViolationException("You can only remove leagues with a 'Rejected' status.");
        
        league.RemoveMember(request.CurrentUserId);

        await leagueRepository.UpdateAsync(league, cancellationToken);
    }
}