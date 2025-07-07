using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin.Teams;

namespace PredictionLeague.Infrastructure.Services;

public class TeamService : ITeamService
{
    private readonly ITeamRepository _teamRepository;

    public TeamService(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }
   
    #region Create

    public async Task<Team> CreateAsync(CreateTeamRequest request)
    {
        var newTeam = new Team
        {
            Name = request.Name, 
            LogoUrl = request.LogoUrl
        };
        
        return await _teamRepository.AddAsync(newTeam);
    }

    #endregion

    #region Read

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

    #endregion

    #region Update

    public async Task UpdateAsync(int id, UpdateTeamRequest request)
    {
        var team = await _teamRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Team not found.");
      
        team.Name = request.Name;
        team.LogoUrl = request.LogoUrl;
        
        await _teamRepository.UpdateAsync(team);
    }
    
    #endregion
}