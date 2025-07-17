using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Leagues;
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
}