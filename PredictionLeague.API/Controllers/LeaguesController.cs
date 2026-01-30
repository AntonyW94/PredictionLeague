using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Leagues.Commands;
using PredictionLeague.Application.Features.Leagues.Queries;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Leagues - Create, join, and manage prediction leagues")]
public class LeaguesController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public LeaguesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Create

    [HttpPost("create")]
    [SwaggerOperation(
        Summary = "Create a new prediction league",
        Description = "Creates a new league with the specified settings. The creating user automatically becomes the league administrator and an approved member. Returns the league details including the generated 6-character entry code.")]
    [SwaggerResponse(201, "League created successfully", typeof(LeagueDto))]
    [SwaggerResponse(400, "Validation failed - invalid name, scoring settings, or season")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> CreateLeagueAsync(
        [FromBody, SwaggerParameter("League configuration including name, visibility, and scoring rules", Required = true)] CreateLeagueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateLeagueCommand(
            request.Name,
            request.SeasonId,
            request.Price,
            CurrentUserId,
            request.EntryDeadlineUtc,
            request.PointsForExactScore,
            request.PointsForCorrectResult);

        var newLeague = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction("GetLeagueById", new { leagueId = newLeague.Id }, newLeague);
    }

    #endregion

    #region Read

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get user's leagues for management",
        Description = "Returns all leagues where the current user is an approved member, with management details.")]
    [SwaggerResponse(200, "Leagues retrieved successfully", typeof(ManageLeaguesDto))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<ManageLeaguesDto>> GetManageLeaguesAsync(CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(nameof(ApplicationUserRole.Administrator));
        var query = new GetManageLeaguesQuery(CurrentUserId, isAdmin);

        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}")]
    [SwaggerOperation(
        Summary = "Get league details",
        Description = "Returns detailed information about a specific league including settings, scoring rules, and the current user's membership status.")]
    [SwaggerResponse(200, "League details retrieved successfully", typeof(LeagueDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueDto>> GetLeagueByIdAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueByIdQuery(leagueId, CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/members")]
    [SwaggerOperation(
        Summary = "Get league members",
        Description = "Returns all members of the league including their status (approved, pending, rejected) and join date. Only approved members can view this.")]
    [SwaggerResponse(200, "Members retrieved successfully", typeof(LeagueMembersPageDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueMembersPageDto>> FetchLeagueMembersAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new FetchLeagueMembersQuery(leagueId, CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("create-data")]
    [SwaggerOperation(
        Summary = "Get league creation form data",
        Description = "Returns data needed to populate the league creation form including available seasons and default scoring values.")]
    [SwaggerResponse(200, "Form data retrieved successfully", typeof(CreateLeaguePageData))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<CreateLeaguePageData>> GetCreateLeaguePageDataAsync(CancellationToken cancellationToken)
    {
        var query = new GetCreateLeaguePageDataQuery();
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/prizes")]
    [SwaggerOperation(
        Summary = "Get league prize settings",
        Description = "Returns the prize distribution configuration for the league including round prizes, monthly prizes, overall prizes, and most exact scores prizes.")]
    [SwaggerResponse(200, "Prize settings retrieved successfully", typeof(LeaguePrizesPageDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeaguePrizesPageDto>> GetLeaguePrizesPageAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeaguePrizesPageQuery(leagueId, CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/rounds/{roundId:int}/results")]
    [SwaggerOperation(
        Summary = "Get round results for league",
        Description = "Returns detailed results for a specific round including each member's predictions, points scored, and ranking. Shows actual match scores and individual prediction breakdowns.")]
    [SwaggerResponse(200, "Round results retrieved successfully", typeof(IEnumerable<PredictionResultDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League or round not found")]
    public async Task<ActionResult<IEnumerable<PredictionResultDto>>> GetLeagueDashboardRoundResultsAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueDashboardRoundResultsQuery(leagueId, roundId, CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/rounds-for-dashboard")]
    [SwaggerOperation(
        Summary = "Get rounds for league dashboard",
        Description = "Returns a summary of rounds for the league dashboard including completed, in-progress, and upcoming rounds with basic stats.")]
    [SwaggerResponse(200, "Rounds retrieved successfully", typeof(IEnumerable<RoundDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<IEnumerable<RoundDto>>> GetLeagueRoundsForDashboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueRoundsForDashboardQuery(leagueId, CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/dashboard-data")]
    [SwaggerOperation(
        Summary = "Get comprehensive league dashboard data",
        Description = "Returns all data needed for the league dashboard page including recent results, standings, upcoming fixtures, and user's prediction status.")]
    [SwaggerResponse(200, "Dashboard data retrieved successfully", typeof(LeagueDashboardDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<LeagueDashboardDto>> GetLeagueDashboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(nameof(ApplicationUserRole.Administrator));
        var query = new GetLeagueDashboardQuery(leagueId, CurrentUserId, isAdmin);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    #region Dashboard

    [HttpGet("{leagueId:int}/months")]
    [SwaggerOperation(
        Summary = "Get months with completed rounds",
        Description = "Returns a list of months that have completed rounds for monthly leaderboard filtering. Only months with at least one completed round are included.")]
    [SwaggerResponse(200, "Months retrieved successfully", typeof(IEnumerable<MonthDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<IEnumerable<MonthDto>>> GetMonthsForLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetMonthsForLeagueQuery(leagueId, CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/leaderboard/overall")]
    [SwaggerOperation(
        Summary = "Get overall season leaderboard",
        Description = "Returns the league leaderboard ranked by total points accumulated across all completed rounds in the season.")]
    [SwaggerResponse(200, "Leaderboard retrieved successfully", typeof(IEnumerable<LeaderboardEntryDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> GetOverallLeaderboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetOverallLeaderboardQuery(leagueId, CurrentUserId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{leagueId:int}/leaderboard/monthly/{month:int}")]
    [SwaggerOperation(
        Summary = "Get monthly leaderboard",
        Description = "Returns the league leaderboard for a specific month, ranked by points accumulated in rounds completed during that month.")]
    [SwaggerResponse(200, "Monthly leaderboard retrieved successfully", typeof(IEnumerable<LeaderboardEntryDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found or no data for specified month")]
    public async Task<ActionResult<IEnumerable<LeaderboardEntryDto>>> GetMonthlyLeaderboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [SwaggerParameter("Month number (1-12)")] int month,
        CancellationToken cancellationToken)
    {
        var query = new GetMonthlyLeaderboardQuery(leagueId, month, CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{leagueId:int}/leaderboard/exact-scores")]
    [SwaggerOperation(
        Summary = "Get exact scores leaderboard",
        Description = "Returns the league leaderboard ranked by number of exact score predictions (where predicted score exactly matched actual score).")]
    [SwaggerResponse(200, "Exact scores leaderboard retrieved successfully", typeof(ExactScoresLeaderboardDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<ExactScoresLeaderboardDto>> GetExactScoresLeaderboardAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetExactScoresLeaderboardQuery(leagueId, CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    #endregion

    #region Winnings

    [HttpGet("{leagueId:int}/winnings")]
    [SwaggerOperation(
        Summary = "Get league winnings",
        Description = "Returns all prize payouts that have been awarded in this league, including round winners, monthly winners, and special prizes.")]
    [SwaggerResponse(200, "Winnings retrieved successfully", typeof(WinningsDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League not found")]
    public async Task<ActionResult<WinningsDto>> GetWinningsAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var query = new GetWinningsQuery(leagueId, CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    #endregion

    #endregion

    #region Update

    [HttpPut("{leagueId:int}/update")]
    [SwaggerOperation(
        Summary = "Update league settings",
        Description = "Updates league configuration. Only the league administrator can perform this action. Scoring rules cannot be changed after predictions have been submitted.")]
    [SwaggerResponse(204, "League updated successfully")]
    [SwaggerResponse(400, "Validation failed or scoring rules locked")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> UpdateLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [FromBody, SwaggerParameter("Updated league settings", Required = true)] UpdateLeagueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateLeagueCommand(
            leagueId,
            request.Name,
            request.Price,
            request.EntryDeadlineUtc,
            CurrentUserId);

        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("join")]
    [SwaggerOperation(
        Summary = "Join league with entry code",
        Description = "Submits a request to join a private league using a 6-character entry code. For public leagues, membership is instant. For private leagues, the request is pending until approved by an administrator.")]
    [SwaggerResponse(204, "Join request submitted successfully")]
    [SwaggerResponse(400, "Invalid entry code or already a member")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> JoinLeagueAsync(
        [FromBody, SwaggerParameter("Entry code for the league", Required = true)] JoinLeagueRequest request,
        CancellationToken cancellationToken)
    {
        var command = new JoinLeagueCommand(CurrentUserId, CurrentUserFirstName, CurrentUserLastName, null, request.EntryCode);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{leagueId:int}/join")]
    [SwaggerOperation(
        Summary = "Join public league directly",
        Description = "Joins a public league directly without an entry code. Only works for public leagues.")]
    [SwaggerResponse(204, "Joined league successfully")]
    [SwaggerResponse(400, "League is private or already a member")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> JoinPublicLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new JoinLeagueCommand(CurrentUserId, CurrentUserFirstName, CurrentUserLastName, leagueId, null);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{leagueId:int}/members/{memberId}/status")]
    [SwaggerOperation(
        Summary = "Update member status",
        Description = "Approves, rejects, or removes a league member. Only the league administrator can perform this action.")]
    [SwaggerResponse(204, "Member status updated successfully")]
    [SwaggerResponse(400, "Invalid status transition")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League or member not found")]
    public async Task<IActionResult> UpdateLeagueMemberStatusAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [SwaggerParameter("Member identifier")] string memberId,
        [FromBody, SwaggerParameter("New membership status", Required = true)] LeagueMemberStatus newStatus,
        CancellationToken cancellationToken)
    {
        var command = new UpdateLeagueMemberStatusCommand(leagueId, memberId, CurrentUserId, newStatus);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{leagueId:int}/prizes")]
    [SwaggerOperation(
        Summary = "Update league prize settings",
        Description = "Configures prize distribution for the league. Only the league administrator can perform this action.")]
    [SwaggerResponse(204, "Prize settings updated successfully")]
    [SwaggerResponse(400, "Invalid prize configuration")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> DefinePrizeStructureAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        [FromBody, SwaggerParameter("Prize distribution configuration", Required = true)] DefinePrizeStructureRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DefinePrizeStructureCommand(leagueId, CurrentUserId, request.PrizeSettings);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{leagueId:int}/join-request")]
    [SwaggerOperation(
        Summary = "Withdraw join request",
        Description = "Cancels a pending request to join a league. Only the user who submitted the request can withdraw it.")]
    [SwaggerResponse(204, "Join request withdrawn successfully")]
    [SwaggerResponse(400, "No pending request found")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> CancelJoinRequestAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new CancelLeagueRequestCommand(leagueId, CurrentUserId);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{leagueId:int}/dismiss-alert")]
    [SwaggerOperation(
        Summary = "Dismiss league alert",
        Description = "Marks an alert or notification for the league as dismissed for the current user.")]
    [SwaggerResponse(204, "Alert dismissed successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not a member of this league")]
    [SwaggerResponse(404, "League or alert not found")]
    public async Task<IActionResult> DismissAlertAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var command = new DismissRejectedNotificationCommand(leagueId, CurrentUserId);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion

    #region Delete

    [HttpDelete("{leagueId:int}")]
    [SwaggerOperation(
        Summary = "Delete league",
        Description = "Permanently deletes a league. Only the league administrator can perform this action, and only if they are the sole member.")]
    [SwaggerResponse(204, "League deleted successfully")]
    [SwaggerResponse(400, "Cannot delete - league has other members")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not the league administrator")]
    [SwaggerResponse(404, "League not found")]
    public async Task<IActionResult> DeleteLeagueAsync(
        [SwaggerParameter("League identifier")] int leagueId,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(nameof(ApplicationUserRole.Administrator));

        var command = new DeleteLeagueCommand(leagueId, CurrentUserId, isAdmin);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    #endregion
}
