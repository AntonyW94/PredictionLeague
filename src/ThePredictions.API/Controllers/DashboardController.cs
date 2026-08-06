using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Dashboard - Aggregated data for the main dashboard view")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class DashboardController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("active-rounds")]
    [SwaggerOperation(
        Summary = "Get active rounds for dashboard",
        Description = "Returns upcoming and in-progress rounds with match counts, deadlines, and prediction status. Used to populate the main dashboard tiles.")]
    [SwaggerResponse(200, "Active rounds retrieved successfully", typeof(IEnumerable<ActiveRoundDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<IEnumerable<ActiveRoundDto>>> GetActiveRoundsAsync(CancellationToken cancellationToken)
    {
        var query = new GetActiveRoundsQuery(CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("my-leagues")]
    [SwaggerOperation(
        Summary = "Get user's leagues",
        Description = "Returns all leagues the current user is a member of, including pending join requests. Shows league name, member count, and user's current standing.")]
    [SwaggerResponse(200, "User's leagues retrieved successfully", typeof(IEnumerable<MyLeagueDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<IEnumerable<MyLeagueDto>>> GetMyLeaguesAsync(CancellationToken cancellationToken)
    {
        var query = new GetMyLeaguesQuery(CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("available-leagues")]
    [SwaggerOperation(
        Summary = "Get public leagues available to join",
        Description = "Returns public leagues for the current season that the user is not already a member of.")]
    [SwaggerResponse(200, "Available leagues retrieved successfully", typeof(IEnumerable<AvailableLeagueDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<IEnumerable<AvailableLeagueDto>>> GetAvailableLeaguesAsync(CancellationToken cancellationToken)
    {
        var query = new GetAvailableLeaguesQuery(CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("private-leagues-available")]
    [SwaggerOperation(
        Summary = "Check if private leagues exist",
        Description = "Returns whether any private leagues are available for the current season. Used to show/hide the 'Join Private League' option.")]
    [SwaggerResponse(200, "Returns boolean indicating private league availability", typeof(bool))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> CheckForAvailablePrivateLeaguesAsync(CancellationToken cancellationToken)
    {
        var query = new CheckForAvailablePrivateLeaguesQuery(CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("leaderboards")]
    [SwaggerOperation(
        Summary = "Get leaderboard summaries",
        Description = "Returns the user's position and points across all their leagues. Used for the dashboard leaderboard summary widget.")]
    [SwaggerResponse(200, "Leaderboard summaries retrieved successfully", typeof(IEnumerable<LeagueLeaderboardDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> GetLeaderboardsAsync(CancellationToken cancellationToken)
    {
        var query = new GetLeaderboardsQuery(CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("pending-members")]
    [SwaggerOperation(
        Summary = "Get pending members for admin",
        Description = "Returns pending league membership requests for leagues the current user administers where the entry deadline has not passed.")]
    [SwaggerResponse(200, "Pending members retrieved successfully", typeof(PendingMembersResultDto))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<PendingMembersResultDto>> GetPendingMembersAsync(CancellationToken cancellationToken)
    {
        var query = new GetPendingMembersForAdminQuery(CurrentUserId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("pending-requests")]
    [SwaggerOperation(
        Summary = "Get pending join requests",
        Description = "Returns pending membership requests for leagues the current user administers. Used to show notification badges.")]
    [SwaggerResponse(200, "Pending requests retrieved successfully", typeof(IEnumerable<LeagueRequestDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<IEnumerable<LeagueRequestDto>>> GetPendingRequestsAsync(CancellationToken cancellationToken)
    {
        var query = new GetPendingRequestsQuery(CurrentUserId);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
