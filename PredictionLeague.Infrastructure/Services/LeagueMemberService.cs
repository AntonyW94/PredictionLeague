using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Leagues;

namespace PredictionLeague.Infrastructure.Services
{
    public class LeagueMemberService : ILeagueMemberService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILeagueRepository _leagueRepository;

        public LeagueMemberService(UserManager<ApplicationUser> userManager, ILeagueRepository leagueRepository)
        {
            _userManager = userManager;
            _leagueRepository = leagueRepository;
        }

        public async Task<IEnumerable<LeagueMemberDto>> GetByLeagueIdAsync(int leagueId)
        {
            var members = await _leagueRepository.GetMembersByLeagueIdAsync(leagueId);
            var membersToReturn = new List<LeagueMemberDto>();

            foreach (var member in members)
            {
                var user = await _userManager.FindByIdAsync(member.UserId);
                if (user != null)
                {
                    membersToReturn.Add(new LeagueMemberDto
                    {
                        UserId = member.UserId,
                        FullName = $"{user.FirstName} {user.LastName}",
                        JoinedAt = member.JoinedAt,
                        Status = member.Status
                    });
                }
            }

            return membersToReturn;
        }
    }
}
