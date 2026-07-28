using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BadgesController(IMediator mediator, IBadgeIconRenderer badgeIconRenderer) : ApiControllerBase
{
    // Public badge icon PNG. Emails cannot render the app's inline SVG badges, so the round-results
    // digest references this instead. Anonymous because email clients/crawlers are not logged in.
    [AllowAnonymous]
    [HttpGet("{key}.png")]
    public IActionResult GetBadgeIcon(string key)
    {
        var png = badgeIconRenderer.Render(key);

        if (png is null)
            return NotFound();

        Response.Headers.CacheControl = "public, max-age=86400";
        return File(png, "image/png");
    }

    [HttpGet]
    public async Task<ActionResult<UserBadgesDto>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetUserBadgesQuery(CurrentUserId), cancellationToken));
    }

    [HttpGet("tile")]
    public async Task<ActionResult<BadgesTileDto>> GetTileAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetBadgesTileQuery(CurrentUserId), cancellationToken));
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<BadgeLeaderboardDto>> GetLeaderboardAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetBadgeLeaderboardQuery(CurrentUserId), cancellationToken));
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<UserBadgesDto>> GetForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetUserBadgesQuery(userId), cancellationToken));
    }
}
