using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Admin.Results;
using PredictionLeague.Shared.Admin.Rounds;
using PredictionLeague.Shared.Admin.Seasons;
using PredictionLeague.Shared.Admin.Teams;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.Application.Services;

public interface IAdminService
{
    // Seasons
    Task<IEnumerable<SeasonDto>> GetAllSeasonsAsync();
    Task CreateSeasonAsync(CreateSeasonRequest request);
    Task UpdateSeasonAsync(int id, UpdateSeasonRequest request);

    // Rounds
    Task<IEnumerable<RoundDto>> GetRoundsForSeasonAsync(int seasonId);
    Task<RoundDetailsDto?> GetRoundByIdAsync(int roundId);
    Task CreateRoundAsync(CreateRoundRequest request);
    Task UpdateRoundAsync(int roundId, UpdateRoundRequest request);

    // Teams
    Task<Team> CreateTeamAsync(CreateTeamRequest request);
    Task UpdateTeamAsync(int id, UpdateTeamRequest request);

    // Leagues
    Task<IEnumerable<LeagueDto>> GetAllLeaguesAsync();
    Task CreateLeagueAsync(CreateLeagueRequest request, string administratorId);
    Task UpdateLeagueAsync(int id, UpdateLeagueRequest request);
    Task<IEnumerable<LeagueMemberDto>> GetLeagueMembersAsync(int leagueId);
    Task ApproveLeagueMemberAsync(int leagueId, string memberId);

    //Matches
    Task UpdateMatchResultsAsync(int roundId, List<UpdateMatchResultsRequest>? results);
}
