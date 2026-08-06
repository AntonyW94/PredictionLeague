using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ThePredictions.Application.Features.Homepage.Queries;
using ThePredictions.Contracts.Homepage;

namespace ThePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[SwaggerTag("Homepage — public data for the landing page")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class HomepageController(IMediator mediator) : ControllerBase
{
    [HttpGet("seasons")]
    [SwaggerOperation(
        Summary = "Get active and upcoming seasons",
        Description = "Returns seasons that are currently in progress or upcoming, for display on the public homepage.")]
    [SwaggerResponse(200, "Active and upcoming seasons", typeof(IEnumerable<HomepageSeasonDto>))]
    public async Task<ActionResult<IEnumerable<HomepageSeasonDto>>> GetSeasonsAsync(
        CancellationToken cancellationToken)
    {
        var query = new GetHomepageSeasonsQuery();
        return Ok(await mediator.Send(query, cancellationToken));
    }
}
