using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Repositories;

public interface IWinningsRepository
{
    Task<decimal> GetUserLeagueTotalAsync(int leagueId, string userId, CancellationToken cancellationToken);
    Task AddWinningsAsync(IEnumerable<Winning> winnings, CancellationToken cancellationToken);
    Task DeleteWinningsForRoundAsync(int leagueId, int roundNumber, CancellationToken cancellationToken);
    Task DeleteWinningsForMonthAsync(int leagueId, int month, CancellationToken cancellationToken);
    Task DeleteWinningsForOverallAsync(int leagueId, CancellationToken cancellationToken);
    Task DeleteWinningsForMostExactScoresAsync(int leagueId, CancellationToken cancellationToken);
    Task DeleteWinningsForStageAsync(int leagueId, string stage, CancellationToken cancellationToken);
}