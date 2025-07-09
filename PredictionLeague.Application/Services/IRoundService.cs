using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin;
using PredictionLeague.Shared.Admin.Rounds;

namespace PredictionLeague.Application.Services;

public interface IRoundService
{
    Task CreateAsync(CreateRoundRequest request);
    Task<RoundDetailsDto?> GetByIdAsync(int roundId);
    Task<IEnumerable<RoundDto>> FetchBySeasonIdAsync(int seasonId);
    Task UpdateAsync(int roundId, UpdateRoundRequest request);
    Task UpdateResultsAsync(int roundId, IEnumerable<Match> completedMatches);
    
}