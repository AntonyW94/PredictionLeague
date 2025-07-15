using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Leagues;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ILeagueMemberService _leagueMemberService;
    private readonly ILeagueService _leagueService;
    private readonly IMatchService _matchService;
    private readonly IRoundService _roundService;
    private readonly ISeasonService _seasonService;
    private readonly ITeamService _teamService;

    public AdminService(
        ILeagueMemberService leagueMemberService,
        ILeagueService leagueService,
        IMatchService matchService,
        IRoundService roundService,
        ISeasonService seasonService,
        ITeamService teamService
        )
    {
        _leagueMemberService = leagueMemberService;
        _leagueService = leagueService;
        _matchService = matchService;
        _roundService = roundService;
        _seasonService = seasonService;
        _teamService = teamService;
    }

    #region Seasons

    public async Task CreateSeasonAsync(CreateSeasonRequest request) => await _seasonService.CreateAsync(request);

    public async Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync() => await _seasonService.GetAllAsync();

    public async Task UpdateSeasonAsync(int id, UpdateSeasonRequest request) => await _seasonService.UpdateAsync(id, request);

    #endregion

    #region Rounds

    public async Task CreateRoundAsync(CreateRoundRequest request) => await _roundService.CreateAsync(request);

    public async Task<RoundDetailsDto?> GetRoundByIdAsync(int roundId) => await _roundService.GetByIdAsync(roundId);

    public async Task<IEnumerable<RoundDto>> GetRoundsForSeasonAsync(int seasonId) => await _roundService.FetchBySeasonIdAsync(seasonId);

    public async Task UpdateRoundAsync(int roundId, UpdateRoundRequest request) => await _roundService.UpdateAsync(roundId, request);

    #endregion

    #region Teams

    public async Task<Team> CreateTeamAsync(CreateTeamRequest request) => await _teamService.CreateAsync(request);

    public async Task UpdateTeamAsync(int id, UpdateTeamRequest request) => await _teamService.UpdateAsync(id, request);

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