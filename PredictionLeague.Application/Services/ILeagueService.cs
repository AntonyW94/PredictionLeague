using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.Application.Services;

public interface ILeagueService
{
    Task<League> CreateAsync(CreateLeagueRequest request, string administratorUserId);
    Task<IEnumerable<LeagueDto>> GetAllAsync();
    Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId);
    Task<League?> GetDefaultPublicLeagueAsync();
    Task UpdateAsync(int id, UpdateLeagueRequest request);
    Task JoinLeagueAsync(string entryCode, string userId);
    Task ApproveLeagueMemberAsync(int leagueId, string memberId);
    Task JoinPublicLeagueAsync(int leagueId, string userId);
}