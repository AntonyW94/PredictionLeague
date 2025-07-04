using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Shared.Admin.Teams;

namespace PredictionLeague.Infrastructure.Services;

public class TeamService : ITeamService
{
    private readonly ITeamRepository _teamRepository;

    public TeamService(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<IEnumerable<TeamDto>> GetAllAsync()
    {
        var teams = await _teamRepository.GetAllAsync();
        return teams.Select(t => new TeamDto
        {
            Id = t.Id,
            Name = t.Name,
            LogoUrl = t.LogoUrl
        });
    }

    public async Task<TeamDto?> GetByIdAsync(int id)
    {
        var team = await _teamRepository.GetByIdAsync(id);
        if (team == null) 
            return null;
            
        return new TeamDto
        {
            Id = team.Id,
            Name = team.Name,
            LogoUrl = team.LogoUrl
        };
    }
}