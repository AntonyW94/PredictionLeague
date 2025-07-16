using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Leagues;
using PredictionLeague.Contracts.Admin.Results;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Models;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IMediator _mediator;

    public AdminController(IAdminService adminService, IMediator mediator)
    {
        _adminService = adminService;
        _mediator = mediator;
    }

    #region Rounds

    [HttpGet("seasons/{seasonId:int}/rounds")]
    public async Task<IActionResult> GetRoundsForSeason(int seasonId)
    {
        return Ok(await _adminService.GetRoundsForSeasonAsync(seasonId));
    }

    [HttpGet("rounds/{roundId:int}")]
    public async Task<IActionResult> GetRoundById(int roundId)
    {
        var roundDetails = await _adminService.GetRoundByIdAsync(roundId);
        if (roundDetails == null)
            return NotFound();

        return Ok(roundDetails);
    }

    [HttpPost("round")]
    public async Task<IActionResult> CreateRound([FromBody] CreateRoundRequest request)
    {
        await _mediator.Send((CreateRoundCommand)request);
        return Ok(new { message = "Round and matches created successfully." });
    }

    [HttpPut("rounds/{roundId:int}")]
    public async Task<IActionResult> UpdateRound(int roundId, [FromBody] UpdateRoundRequest request)
    {
        var command = new UpdateRoundCommand
        {
            RoundId = roundId,
            StartDate = request.StartDate,
            Deadline = request.Deadline,
            Matches = request.Matches
        };

        await _mediator.Send(command);

        return Ok(new { message = "Round updated successfully." });
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

    [HttpGet("leagues/{leagueId:int}/members")]
    public async Task<IActionResult> GetLeagueMembers(int leagueId)
    {
        return Ok(await _adminService.GetLeagueMembersAsync(leagueId));
    }

    [HttpPost("leagues/{leagueId:int}/members/{memberId}/approve")]
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