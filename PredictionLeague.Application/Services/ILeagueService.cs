using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.Application.Services;

public interface ILeagueService
{
    Task<League> CreateLeagueAsync(CreateLeagueRequest request, string administratorUserId);
    Task JoinLeagueAsync(string entryCode, string userId);
    Task ApproveLeagueMemberAsync(int leagueId, string memberId);
    Task JoinPublicLeagueAsync(int leagueId, string userId);
    Task<IEnumerable<League>> GetLeaguesForUserAsync(string userId);
    Task<League?> GetDefaultPublicLeagueAsync();
}