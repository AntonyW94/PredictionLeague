using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class UpdateLeagueMemberStatusCommandHandler(ILeagueRepository leagueRepository, ILeagueMemberRepository leagueMemberRepository, ILeagueStatsRepository leagueStatsRepository, IMediator mediator, IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateLeagueMemberStatusCommand>
{
    public async Task Handle(UpdateLeagueMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        if (league.AdministratorUserId != request.UpdatingUserId)
            throw new UnauthorizedAccessException("Only the league administrator can update member status.");
        
        var member = await leagueMemberRepository.GetAsync(request.LeagueId, request.MemberId, cancellationToken);
        Guard.Against.EntityNotFound(request.MemberId, member, "LeagueMember");

        switch (request.NewStatus)
        {
            case LeagueMemberStatus.Approved:
                member.Approve(dateTimeProvider);
                break;

            case LeagueMemberStatus.Rejected:
                member.Reject();
                break;

            case LeagueMemberStatus.Pending:
                break;
            
            default:
                throw new BusinessRuleViolationException("This status change is not permitted.");
        }

        await leagueMemberRepository.UpdateAsync(member, cancellationToken);

        // A rank is a position relative to the other members, so admitting someone moves everybody
        // else's cached rank too, not just the new member's. Without this the whole league's tiles stay
        // wrong until the next results update.
        //
        // Only approval needs this. LeagueMember.Approve/Reject both require the member to be Pending,
        // and a pending member is not ranked, so rejection cannot move anyone. Nothing in the app can
        // take an already-approved member back out of a league; if that is ever added, it needs a
        // refresh too (the recompute itself already handles it - it derives the ranked set from who is
        // currently approved rather than from what changed).
        var isApproved = request.NewStatus == LeagueMemberStatus.Approved;
        if (isApproved)
            await leagueStatsRepository.RefreshLeagueAsync(league.Id, cancellationToken);

        // Let the member know they can now take part once the admin has approved them.
        if (isApproved)
            await mediator.Send(new NotifyMemberOfLeagueApprovalCommand(member.UserId, league.Id, league.Name, league.SeasonId), cancellationToken);
    }
}