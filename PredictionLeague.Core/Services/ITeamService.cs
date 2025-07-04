using PredictionLeague.Shared.Admin.Teams;

namespace PredictionLeague.Core.Services;

public interface ITeamService
{
    Task<IEnumerable<TeamDto>> GetAllAsync();
    Task<TeamDto?> GetByIdAsync(int id);
}