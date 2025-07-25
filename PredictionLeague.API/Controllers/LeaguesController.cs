using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Leagues.Commands;
using PredictionLeague.Application.Features.Leagues.Queries;
using PredictionLeague.Contracts;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;

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
    [ProducesResponseType(typeof(LeagueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLeagueAsync([FromBody] CreateLeagueRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateLeagueCommand(
            request.Name,
            request.SeasonId,
            request.Price,
            CurrentUserId,
            request.EntryDeadline);

        var newLeague = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetLeagueById", new { leagueId = newLeague.Id }, newLeague);
    }

    #endregion

    #region Read

    [HttpGet]
    [ProducesResponseType(typeof(ManageLeaguesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ManageLeaguesDto>> GetManageLeaguesAsync(CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(RoleNames.Administrator);
        var query = new GetManageLeaguesQuery(CurrentUserId, isAdmin);

        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}")]
    [ProducesResponseType(typeof(LeagueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeagueDto>> GetLeagueByIdAsync(int leagueId, CancellationToken cancellationToken)
    {
        var query = new GetLeagueByIdQuery(leagueId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/members")]
    [ProducesResponseType(typeof(LeagueMembersPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeagueMembersPageDto>> FetchLeagueMembersAsync(int leagueId, CancellationToken cancellationToken)
    {
        var query = new FetchLeagueMembersQuery(leagueId, CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("create-data")]
    [ProducesResponseType(typeof(CreateLeaguePageData), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateLeaguePageData>> GetCreateLeaguePageDataAsync(CancellationToken cancellationToken)
    {
        var query = new GetCreateLeaguePageDataQuery();
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    #endregion

    #region Update

    [HttpPut("{leagueId:int}/update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLeagueAsync(int leagueId, [FromBody] UpdateLeagueRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateLeagueCommand(
            leagueId, 
            request.Name,
            request.Price, 
            request.EntryDeadline);
        
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("join")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinLeagueAsync([FromBody] JoinLeagueRequest request, CancellationToken cancellationToken)
    {
        var command = new JoinLeagueCommand(CurrentUserId, null, request.EntryCode);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{leagueId:int}/join")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinPublicLeagueAsync(int leagueId, CancellationToken cancellationToken)
    {
        var command = new JoinLeagueCommand(CurrentUserId, leagueId, null);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{leagueId:int}/members/{memberId}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLeagueMemberStatusAsync(int leagueId, string memberId, [FromBody] LeagueMemberStatus newStatus, CancellationToken cancellationToken)
    {
        var command = new UpdateLeagueMemberStatusCommand(leagueId, memberId, CurrentUserId, newStatus);
        await _mediator.Send(command, cancellationToken);
       
        return NoContent();
    }

    #endregion


    #region Delete

    [HttpDelete("{leagueId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteLeagueAsync(int leagueId, CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(RoleNames.Administrator);

        var command = new DeleteLeagueCommand(leagueId, CurrentUserId, isAdmin);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Dashboard

    [HttpGet("{leagueId:int}/leaderboard/overall")]
    [ProducesResponseType(typeof(IEnumerable<LeaderboardEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> GetOverallLeaderboardAsync(int leagueId, CancellationToken cancellationToken)
    {
        var query = new GetOverallLeaderboardQuery(leagueId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }


    [HttpGet("{leagueId:int}/leaderboard/monthly/{month:int}")]
    [ProducesResponseType(typeof(IEnumerable<LeaderboardEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> GetMonthlyLeaderboardAsync(int leagueId, int month, CancellationToken cancellationToken)
    {
        var query = new GetMonthlyLeaderboardQuery(leagueId, month);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/leaderboard/round/{roundId:int}")]
    [ProducesResponseType(typeof(IEnumerable<LeaderboardEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> GetRoundLeaderboardAsync(int leagueId, int roundId, CancellationToken cancellationToken)
    {
        var query = new GetRoundLeaderboardQuery(leagueId, roundId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    #endregion
}