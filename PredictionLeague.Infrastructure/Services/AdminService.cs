using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Leagues;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ILeagueMemberService _leagueMemberService;
    private readonly ILeagueService _leagueService;
    private readonly IMatchService _matchService;
    private readonly IRoundService _roundService;

    public AdminService(
        ILeagueMemberService leagueMemberService,
        ILeagueService leagueService,
        IMatchService matchService,
        IRoundService roundService
        )
    {
        _leagueMemberService = leagueMemberService;
        _leagueService = leagueService;
        _matchService = matchService;
        _roundService = roundService;
    }

    #region Rounds

    public async Task<RoundDetailsDto?> GetRoundByIdAsync(int roundId) => await _roundService.GetByIdAsync(roundId);

    public async Task<IEnumerable<RoundDto>> GetRoundsForSeasonAsync(int seasonId) => await _roundService.FetchBySeasonIdAsync(seasonId);

    #endregion

    #region Leagues

    public async Task CreateLeagueAsync(CreateLeagueRequest request, string administratorId) => await _leagueService.CreateAsync(request, administratorId);

    public async Task<IEnumerable<LeagueDto>> GetAllLeaguesAsync() => await _leagueService.GetAllAsync();

    public async Task<IEnumerable<LeagueMemberDto>> GetLeagueMembersAsync(int leagueId) => await _leagueMemberService.GetByLeagueIdAsync(leagueId);

    public async Task UpdateLeagueAsync(int id, UpdateLeagueRequest request) => await _leagueService.UpdateAsync(id, request);

    public async Task ApproveLeagueMemberAsync(int leagueId, string memberId) => await _leagueService.ApproveLeagueMemberAsync(leagueId, memberId);

    #endregion

    #region Matches

    public async Task UpdateMatchResultsAsync(int roundId, List<UpdateMatchResultsRequest>? results) => await _matchService.UpdateMatchResultsAsync(roundId, results);

    #endregion
}