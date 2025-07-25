using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Common.Guards.Season;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class ApproveLeagueMemberCommandHandler : IRequestHandler<ApproveLeagueMemberCommand>
{
    private readonly ILeagueRepository _leagueRepository;

    public ApproveLeagueMemberCommandHandler(ILeagueRepository leagueRepository)
    {
        _leagueRepository = leagueRepository;
    }

    public async Task Handle(ApproveLeagueMemberCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");
       
        league.ApproveMember(request.MemberId, request.ApprovingUserId);
       
        await _leagueRepository.UpdateMemberStatusAsync(
            request.LeagueId,
            request.MemberId,
            LeagueMemberStatus.Approved,
            cancellationToken);
    }
}