using MediatR;
using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class FetchLeagueMembersQueryHandler : IRequestHandler<FetchLeagueMembersQuery, LeagueMembersPageDto?>
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public FetchLeagueMembersQueryHandler(ILeagueRepository leagueRepository, UserManager<ApplicationUser> userManager)
    {
        _leagueRepository = leagueRepository;
        _userManager = userManager;
    }

    public async Task<LeagueMembersPageDto?> Handle(FetchLeagueMembersQuery request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdAsync(request.LeagueId);
        if (league == null)
            return null;

        var members = await _leagueRepository.GetMembersByLeagueIdAsync(request.LeagueId);
        var memberDtos = new List<LeagueMemberDto>();

        foreach (var member in members)
        {
            var user = await _userManager.FindByIdAsync(member.UserId);
            if (user != null)
            {
                memberDtos.Add(new LeagueMemberDto
                {
                    UserId = member.UserId,
                    FullName = $"{user.FirstName} {user.LastName}",
                    JoinedAt = member.JoinedAt,
                    Status = member.Status,
                    CanBeApproved = member.Status == LeagueMemberStatus.Pending && league.AdministratorUserId == member.UserId
                });
            }
        }

        return new LeagueMembersPageDto
        {
            LeagueName = league.Name,
            Members = memberDtos.OrderBy(m => m.FullName).ToList()
        };
    }
}