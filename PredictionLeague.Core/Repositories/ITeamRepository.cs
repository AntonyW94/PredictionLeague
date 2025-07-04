using PredictionLeague.Core.Models;

namespace PredictionLeague.Core.Repositories;

public interface ITeamRepository
{
    Task<IEnumerable<Team>> GetAllAsync(); 
    Task<Team?> GetByIdAsync(int id);
    Task UpdateAsync(Team team);
    Task<Team> AddAsync(Team team);
}