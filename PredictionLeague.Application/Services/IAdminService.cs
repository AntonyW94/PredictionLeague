using PredictionLeague.Contracts.Admin.Leagues;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Services;

public interface IAdminService
{
    // Leagues
    Task<IEnumerable<LeagueDto>> GetAllLeaguesAsync();
    Task CreateLeagueAsync(CreateLeagueRequest request, string administratorId);
    Task UpdateLeagueAsync(int id, UpdateLeagueRequest request);
    Task<IEnumerable<LeagueMemberDto>> GetLeagueMembersAsync(int leagueId);
    Task ApproveLeagueMemberAsync(int leagueId, string memberId);
}
