using Ardalis.GuardClauses;
using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;

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
        Guard.Against.NotFound(request.LeagueId, league, $"League with ID {request.LeagueId} not found.");
       
        league.ApproveMember(request.MemberId, request.ApprovingUserId);
       
        await _leagueRepository.UpdateMemberStatusAsync(
            request.LeagueId,
            request.MemberId,
            LeagueMemberStatus.Approved,
            cancellationToken);
    }
}