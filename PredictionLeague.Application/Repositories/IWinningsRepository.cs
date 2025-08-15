using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Repositories;

public interface IWinningsRepository
{
    Task AddWinningsAsync(IEnumerable<Winning> winnings, CancellationToken cancellationToken);
    Task DeleteWinningsForRoundAsync(int leagueId, int roundNumber, CancellationToken cancellationToken);
    Task DeleteWinningsForMonthAsync(int leagueId, int month, CancellationToken cancellationToken); 
    Task DeleteWinningsForEndOfSeasonAsync(int leagueId, CancellationToken cancellationToken);
}