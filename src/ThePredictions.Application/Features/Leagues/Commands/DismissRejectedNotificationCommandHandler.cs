using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class DismissRejectedNotificationCommandHandler(ILeagueMemberRepository leagueMemberRepository) : IRequestHandler<DismissRejectedNotificationCommand>
{
    public async Task Handle(DismissRejectedNotificationCommand request, CancellationToken cancellationToken)
    {
        var member = await leagueMemberRepository.GetAsync(request.LeagueId, request.UserId, cancellationToken);
        Guard.Against.EntityNotFound(request.UserId, member, "League Notification");

        if (member.Status != LeagueMemberStatus.Rejected)
            throw new BusinessRuleViolationException("This notification cannot be dismissed.");

        member.DismissAlert();

        await leagueMemberRepository.UpdateAsync(member, cancellationToken);
    }
}