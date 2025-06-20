using PredictionLeague.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PredictionLeague.Core.Repositories
{
    public interface IMatchRepository
    {
        Task<Match?> GetByIdAsync(int id);
        Task<IEnumerable<Match>> GetByGameWeekIdAsync(int gameWeekId);
        Task AddAsync(Match match);
        Task UpdateAsync(Match match);
    }
}
