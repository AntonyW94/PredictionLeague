using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Teams;

namespace PredictionLeague.Application.Services;

public interface ITeamService
{
    Task<Team> CreateAsync(CreateTeamRequest request);
    Task<IEnumerable<TeamDto>> GetAllAsync();
    Task<TeamDto?> GetByIdAsync(int id);
    Task UpdateAsync(int id, UpdateTeamRequest request);
}