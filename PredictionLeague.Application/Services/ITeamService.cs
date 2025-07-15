using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Services;

public interface ITeamService
{
    Task<Team> CreateAsync(CreateTeamRequest request);
    Task<IEnumerable<TeamDto>> GetAllAsync();
    Task<TeamDto?> GetByIdAsync(int id);
    Task UpdateAsync(int id, UpdateTeamRequest request);
}