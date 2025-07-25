using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class UpdateLeagueMemberStatusCommandHandler : IRequestHandler<UpdateLeagueMemberStatusCommand>
{
    private readonly ILeagueRepository _leagueRepository;

    public UpdateLeagueMemberStatusCommandHandler(ILeagueRepository leagueRepository)
    {
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(UpdateLeagueMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");
      
        switch (request.NewStatus)
        {
            case LeagueMemberStatus.Approved:
                league.ApproveMember(request.MemberId, request.UpdatingUserId);
                break;
            
            case LeagueMemberStatus.Rejected:
                league.RejectMember(request.MemberId, request.UpdatingUserId);
                break;
            
            default:
                throw new InvalidOperationException("This status change is not permitted.");
        }
        
        await _leagueRepository.UpdateMemberStatusAsync(
            request.LeagueId,
            request.MemberId,
            request.NewStatus,
            cancellationToken);
    }
}