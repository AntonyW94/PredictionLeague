using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BadgesController(IMediator mediator) : ApiControllerBase
{
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
}
