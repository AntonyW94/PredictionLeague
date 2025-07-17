using MediatR;
using PredictionLeague.Application.Repositories;

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
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId) ?? throw new KeyNotFoundException($"League with ID {request.LeagueId} not found.");

        if (league.AdministratorUserId != request.ApprovingUserId)
            throw new UnauthorizedAccessException("You are not authorized to approve members for this league.");

        await _leagueRepository.UpdateMemberStatusAsync(request.LeagueId, request.MemberId, "Approved");
    }
}