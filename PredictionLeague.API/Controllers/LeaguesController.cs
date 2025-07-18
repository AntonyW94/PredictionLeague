using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Leagues.Commands;
using PredictionLeague.Application.Features.Leagues.Queries;
using PredictionLeague.Contracts.Admin.Leagues;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LeaguesController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public LeaguesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create

    [HttpPost("create")]
    public async Task<IActionResult> CreateLeague([FromBody] CreateLeagueRequest request)
    {
        var command = new CreateLeagueCommand(request, CurrentUserId);
        var newLeague = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetLeagueById), new { leagueId = newLeague.Id }, newLeague);
    }

    #endregion

    #region Read

    [HttpGet]
    public async Task<IActionResult> FetchAll()
    {
        var query = new FetchAllLeaguesQuery();

        return Ok(await _mediator.Send(query));
    }

    [HttpGet("{leagueId:int}")]
    public async Task<IActionResult> GetLeagueById(int leagueId)
    {
        var query = new GetLeagueByIdQuery(leagueId);
        var result = await _mediator.Send(query);

        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{leagueId:int}/members")]
    [Authorize]
    public async Task<IActionResult> FetchLeagueMembers(int leagueId)
    {
        var query = new FetchLeagueMembersQuery(leagueId);
        var result = await _mediator.Send(query);

        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("create-data")]
    [Authorize]
    public async Task<IActionResult> GetCreateLeaguePageData()
    {
        var query = new GetCreateLeaguePageDataQuery();
        var pageData = await _mediator.Send(query);

        return Ok(pageData);
    }

    #endregion

    #region Update

    [HttpPut("{leagueId:int}/update")]
    public async Task<IActionResult> UpdateLeague(int leagueId, [FromBody] UpdateLeagueRequest request)
    {
        var command = new UpdateLeagueCommand(leagueId, request);
        await _mediator.Send(command);

        return Ok(new { message = "League updated successfully." });
    }

    [HttpPost("join")]
    [Authorize]
    public async Task<IActionResult> JoinLeague([FromBody] JoinLeagueRequest request)
    {
        var command = new JoinLeagueCommand(request.EntryCode, CurrentUserId);

        try
        {
            await _mediator.Send(command);
            return Ok(new { message = "Successfully joined league." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{leagueId:int}/join")]
    [Authorize]
    public async Task<IActionResult> JoinPublicLeague(int leagueId)
    {
        var command = new JoinLeagueCommand(leagueId, CurrentUserId);

        try
        {
            await _mediator.Send(command);
            return Ok(new { message = "Successfully joined league." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{leagueId:int}/members/{memberId}/approve")]
    [Authorize]
    public async Task<IActionResult> ApproveLeagueMember(int leagueId, string memberId)
    {
        var command = new ApproveLeagueMemberCommand(leagueId, memberId, CurrentUserId);

        try
        {
            await _mediator.Send(command);
            return Ok(new { message = "Member approved successfully." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    #endregion

    #region Dashboard

    [HttpGet("{leagueId:int}/leaderboard/overall")]
    public async Task<IActionResult> GetOverallLeaderboard(int leagueId)
    {
        var query = new GetOverallLeaderboardQuery(leagueId);
        var result = await _mediator.Send(query);

        return Ok(result);
    }


    [HttpGet("{leagueId:int}/leaderboard/monthly/{month:int}")]
    public async Task<IActionResult> GetMonthlyLeaderboard(int leagueId, int month)
    {
        var query = new GetMonthlyLeaderboardQuery(leagueId, month);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/leaderboard/round/{roundId:int}")]
    public async Task<IActionResult> GetRoundLeaderboard(int leagueId, int roundId)
    {
        var query = new GetRoundLeaderboardQuery(leagueId, roundId);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    #endregion
}