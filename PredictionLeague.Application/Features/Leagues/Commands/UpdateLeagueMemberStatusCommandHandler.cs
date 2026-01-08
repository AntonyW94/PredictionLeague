using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class UpdateLeagueMemberStatusCommandHandler : IRequestHandler<UpdateLeagueMemberStatusCommand>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ILeagueMemberRepository _leagueMemberRepository;

    public UpdateLeagueMemberStatusCommandHandler(ILeagueRepository leagueRepository, ILeagueMemberRepository leagueMemberRepository)
    {
        _leagueRepository = leagueRepository;
        _leagueMemberRepository = leagueMemberRepository;
    }

    public async Task Handle(UpdateLeagueMemberStatusCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        if (league.AdministratorUserId != request.UpdatingUserId)
            throw new UnauthorizedAccessException("Only the league administrator can update member status.");
        
        var member = await _leagueMemberRepository.GetAsync(request.LeagueId, request.MemberId, cancellationToken);
        Guard.Against.EntityNotFound(request.MemberId, member, "LeagueMember");

        switch (request.NewStatus)
        {
            case LeagueMemberStatus.Approved:
                member.Approve();
                break;

            case LeagueMemberStatus.Rejected:
                member.Reject();
                break;

            case LeagueMemberStatus.Pending:
                break;
            
            default:
                throw new InvalidOperationException("This status change is not permitted.");
        }

        await _leagueMemberRepository.UpdateAsync(member, cancellationToken);
    }
}