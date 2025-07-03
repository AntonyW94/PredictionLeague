using PredictionLeague.Core.Models;
using PredictionLeague.Shared.Admin.Rounds;

namespace PredictionLeague.Core.Repositories
{
    namespace PredictionLeague.Core.Repositories
    {
        public interface IRoundRepository
        {
            Task<Round> AddAsync(Round round);
            Task<IEnumerable<RoundDto>> GetBySeasonIdAsync(int seasonId);
            Task<Round?> GetCurrentRoundAsync(int seasonId);
            Task<Round?> GetByIdAsync(int id);
            Task UpdateAsync(Round round);
            
        }
    }
}
