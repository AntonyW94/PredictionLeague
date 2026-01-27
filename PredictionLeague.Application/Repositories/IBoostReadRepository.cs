using PredictionLeague.Contracts.Boosts;
using PredictionLeague.Domain.Models;
using PredictionLeague.Domain.Services.Boosts;

namespace PredictionLeague.Application.Repositories;

public interface IBoostReadRepository
{
    Task<(int SeasonId, int RoundNumber, DateTime DeadlineUtc)> GetRoundInfoAsync(int roundId, CancellationToken cancellationToken);
    Task<int?> GetLeagueSeasonIdAsync(int leagueId, CancellationToken cancellationToken);
    Task<IEnumerable<BoostDefinition>> GetBoostDefinitionsForLeagueAsync(int leagueId, CancellationToken cancellationToken);
    Task<bool> IsUserMemberOfLeagueAsync(string userId, int leagueId, CancellationToken cancellationToken);
    Task<LeagueBoostRuleSnapshot?> GetLeagueBoostRuleAsync(int leagueId, string boostCode, CancellationToken cancellationToken);
    Task<BoostUsageSnapshot> GetUserBoostUsageSnapshotAsync(string userId, int leagueId, int seasonId, int roundId, string boostCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserRoundBoostDto>> GetBoostsForRoundAsync(int roundId, CancellationToken cancellationToken);
}