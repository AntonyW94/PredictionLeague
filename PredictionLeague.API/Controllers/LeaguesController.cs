using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Leagues.Commands;
using PredictionLeague.Application.Features.Leagues.Queries;
using PredictionLeague.Contracts.Leaderboards;
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
    [ProducesResponseType(typeof(LeagueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLeagueAsync([FromBody] CreateLeagueRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateLeagueCommand(
            request.Name,
            request.SeasonId,
            CurrentUserId,
            request.EntryCode,
            request.EntryDeadline);

        var newLeague = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetLeagueById", new { leagueId = newLeague.Id }, newLeague);
    }

    #endregion

    #region Read

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LeagueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeagueDto>>> FetchAllAsync(CancellationToken cancellationToken)
    {
        var query = new FetchAllLeaguesQuery();
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
        var query = new FetchLeagueMembersQuery(leagueId);
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
        var command = new UpdateLeagueCommand(leagueId, request);
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

    [HttpPost("{leagueId:int}/members/{memberId}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveLeagueMemberAsync(int leagueId, string memberId, CancellationToken cancellationToken)
    {
        var command = new ApproveLeagueMemberCommand(leagueId, memberId, CurrentUserId);
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