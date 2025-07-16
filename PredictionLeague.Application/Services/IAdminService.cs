using PredictionLeague.Contracts.Admin.Leagues;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Services;

public interface IAdminService
{
    // Rounds
    Task<IEnumerable<RoundDto>> GetRoundsForSeasonAsync(int seasonId);
    Task<RoundDetailsDto?> GetRoundByIdAsync(int roundId);

    // Leagues
    Task<IEnumerable<LeagueDto>> GetAllLeaguesAsync();
    Task CreateLeagueAsync(CreateLeagueRequest request, string administratorId);
    Task UpdateLeagueAsync(int id, UpdateLeagueRequest request);
    Task<IEnumerable<LeagueMemberDto>> GetLeagueMembersAsync(int leagueId);
    Task ApproveLeagueMemberAsync(int leagueId, string memberId);

    //Matches
    Task UpdateMatchResultsAsync(int roundId, List<UpdateMatchResultsRequest>? results);
}
