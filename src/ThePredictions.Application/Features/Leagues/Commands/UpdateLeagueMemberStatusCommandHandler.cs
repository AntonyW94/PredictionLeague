using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class UpdateLeagueMemberStatusCommandHandler(ILeagueRepository leagueRepository, ILeagueMemberRepository leagueMemberRepository, IMediator mediator, IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateLeagueMemberStatusCommand>
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
                throw new InvalidOperationException("This status change is not permitted.");
        }

        await leagueMemberRepository.UpdateAsync(member, cancellationToken);

        // Let the member know they can now take part once the admin has approved them.
        if (request.NewStatus == LeagueMemberStatus.Approved)
            await mediator.Send(new NotifyMemberOfLeagueApprovalCommand(member.UserId, league.Name, league.SeasonId), cancellationToken);
    }
}