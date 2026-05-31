using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.SeasonPasses.Commands;
using ThePredictions.Application.Features.SeasonPasses.Queries;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SeasonPassesController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<MySeasonPassDto>>> GetMineAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetMySeasonPassesQuery(CurrentUserId), cancellationToken));
    }

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<AvailableSeasonPassDto>>> GetAvailableAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetAvailableSeasonPassesQuery(CurrentUserId), cancellationToken));
    }

    [HttpGet("options")]
    public async Task<ActionResult<SeasonPassOptionsDto>> GetOptionsAsync([FromQuery] int seasonId, CancellationToken cancellationToken)
    {
        var options = await mediator.Send(new GetSeasonPassOptionsQuery(CurrentUserId, seasonId), cancellationToken);
        return options is null ? NotFound() : Ok(options);
    }

    [HttpPost("acquire")]
    public async Task<IActionResult> AcquireAsync([FromBody] AcquireSeasonPassRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AcquireSeasonPassCommand(CurrentUserId, request.SeasonId), cancellationToken);
        return NoContent();
    }
}
