using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface ILeagueMemberRepository
{
    Task<LeagueMember?> GetAsync(int leagueId, string userId, CancellationToken cancellationToken);
    Task UpdateAsync(LeagueMember member, CancellationToken cancellationToken);
    Task DeleteAsync(LeagueMember member, CancellationToken cancellationToken);
}