using PredictionLeague.Shared.Admin.Teams;

namespace PredictionLeague.Application.Services;

public interface ITeamService
{
    Task<IEnumerable<TeamDto>> GetAllAsync();
    Task<TeamDto?> GetByIdAsync(int id);
}