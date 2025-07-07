using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Admin;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Admin.Results;
using PredictionLeague.Shared.Admin.Rounds;
using PredictionLeague.Shared.Admin.Seasons;
using PredictionLeague.Shared.Admin.Teams;
using PredictionLeague.Shared.Leagues;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    #region Seasons

    [HttpGet("seasons")]
    public async Task<IActionResult> GetAllSeasons()
    {
        var seasons = await _adminService.GetAllSeasonsAsync();
        return Ok(seasons);
    }

    [HttpPost("seasons")]
    public async Task<IActionResult> CreateSeason([FromBody] CreateSeasonRequest request)
    {
        await _adminService.CreateSeasonAsync(request);
        return Ok(new { message = "Season created successfully." });
    }

    [HttpPut("seasons/{id:int}")]
    public async Task<IActionResult> UpdateSeason(int id, [FromBody] UpdateSeasonRequest request)
    {
        await _adminService.UpdateSeasonAsync(id, request);
        return Ok(new { message = "Season updated successfully." });
    }

    #endregion

    #region Rounds

    [HttpGet("seasons/{seasonId:int}/rounds")]
    public async Task<IActionResult> GetRoundsForSeason(int seasonId)
    {
        var rounds = await _adminService.GetRoundsForSeasonAsync(seasonId);
        return Ok(rounds);
    }

    [HttpGet("rounds/{roundId:int}")]
    public async Task<IActionResult> GetRoundById(int roundId)
    {
        try
        {
            var roundDetails = await _adminService.GetRoundByIdAsync(roundId);
            return Ok(roundDetails);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("round")]
    public async Task<IActionResult> CreateRound([FromBody] CreateRoundRequest request)
    {
        await _adminService.CreateRoundAsync(request);
        return Ok(new { message = "Round and matches created successfully." });
    }

    [HttpPut("rounds/{roundId:int}")]
    public async Task<IActionResult> UpdateRound(int roundId, [FromBody] UpdateRoundRequest request)
    {
        await _adminService.UpdateRoundAsync(roundId, request);
        return Ok(new { message = "Round updated successfully." });
    }

    #endregion

    #region Teams

    [HttpPost("teams")]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest request)
    {
        var createdTeam = await _adminService.CreateTeamAsync(request);
        return CreatedAtAction(nameof(TeamsController.GetTeamById), "Teams", new { id = createdTeam.Id }, createdTeam);
    }

    [HttpPut("teams/{id:int}")]
    public async Task<IActionResult> UpdateTeam(int id, [FromBody] UpdateTeamRequest request)
    {
        await _adminService.UpdateTeamAsync(id, request);
        return Ok(new { message = "Team updated successfully." });
    }

    #endregion

    #region Leagues

    [HttpGet("leagues")]
    public async Task<IActionResult> GetAllLeagues()
    {
        var leagues = await _adminService.GetAllLeaguesAsync();
        return Ok(leagues);
    }

    [HttpPost("leagues")]
    public async Task<IActionResult> CreateLeague([FromBody] CreateLeagueRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _adminService.CreateLeagueAsync(request, userId!);
        return Ok(new { message = "League created successfully." });
    }

    [HttpPut("leagues/{id:int}")]
    public async Task<IActionResult> UpdateLeague(int id, [FromBody] UpdateLeagueRequest request)
    {
        await _adminService.UpdateLeagueAsync(id, request);
        return Ok(new { message = "League updated successfully." });
    }

    [HttpGet("leagues/{leagueId}/members")]
    public async Task<IActionResult> GetLeagueMembers(int leagueId)
    {
        var members = await _adminService.GetLeagueMembersAsync(leagueId);
        return Ok(members);
    }

    [HttpPost("leagues/{leagueId}/members/{memberId}/approve")]
    public async Task<IActionResult> ApproveLeagueMember(int leagueId, string memberId)
    {
        await _adminService.ApproveLeagueMemberAsync(leagueId, memberId);
        return Ok(new { message = "Member approved successfully." });
    }
    
    #endregion

    #region Matches

    [HttpPut("rounds/{roundId:int}/submit-results")]
    public async Task<IActionResult> SubmitResults(int roundId, [FromBody] List<UpdateMatchResultsRequest>? request)
    {
        await _adminService.UpdateMatchResultsAsync(roundId, request);
        return Ok(new { message = "Results updated and points calculated successfully." });
    }

    #endregion
}