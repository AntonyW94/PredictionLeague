using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Onboarding.Commands;
using ThePredictions.Application.Features.Onboarding.Queries;
using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OnboardingController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OnboardingChecklistDto>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetOnboardingChecklistQuery(CurrentUserId), cancellationToken));
    }

    [HttpPost("skip/{stepKey}")]
    public async Task<IActionResult> SkipAsync(string stepKey, CancellationToken cancellationToken)
    {
        await mediator.Send(new SkipOnboardingStepCommand(CurrentUserId, stepKey), cancellationToken);
        return NoContent();
    }

    [HttpPost("dismiss")]
    public async Task<IActionResult> DismissAsync(CancellationToken cancellationToken)
    {
        await mediator.Send(new DismissOnboardingCommand(CurrentUserId), cancellationToken);
        return NoContent();
    }
}
