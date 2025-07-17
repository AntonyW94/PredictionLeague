using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Leagues;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ILeagueMemberService _leagueMemberService;
    private readonly ILeagueService _leagueService;

    public AdminService(
        ILeagueMemberService leagueMemberService,
        ILeagueService leagueService
        )
    {
        _leagueMemberService = leagueMemberService;
        _leagueService = leagueService;
    }

    #region Leagues

    public async Task CreateLeagueAsync(CreateLeagueRequest request, string administratorId) => await _leagueService.CreateAsync(request, administratorId);

    public async Task<IEnumerable<LeagueDto>> GetAllLeaguesAsync() => await _leagueService.GetAllAsync();

    public async Task<IEnumerable<LeagueMemberDto>> GetLeagueMembersAsync(int leagueId) => await _leagueMemberService.GetByLeagueIdAsync(leagueId);

    public async Task UpdateLeagueAsync(int id, UpdateLeagueRequest request) => await _leagueService.UpdateAsync(id, request);

    public async Task ApproveLeagueMemberAsync(int leagueId, string memberId) => await _leagueService.ApproveLeagueMemberAsync(leagueId, memberId);

    #endregion
}