using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService) 
    {
        _teamService = teamService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTeams()
    {
        var teams = await _teamService.GetAllAsync();
        return Ok(teams);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTeamById(int id)
    {
        var team = await _teamService.GetByIdAsync(id);
        if (team == null)
            return NotFound();
           
        return Ok(team);
    }
}