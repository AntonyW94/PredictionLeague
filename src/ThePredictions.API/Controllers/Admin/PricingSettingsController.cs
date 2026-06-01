using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.PricingSettings.Commands;
using ThePredictions.Application.Features.Admin.PricingSettings.Queries;
using ThePredictions.Contracts.Admin.PricingSettings;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Pricing Settings - tune the recommended-price calculator (Admin only)")]
public class PricingSettingsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get the pricing-calculator settings")]
    [SwaggerResponse(200, "Settings returned", typeof(PricingSettingsDto))]
    public async Task<ActionResult<PricingSettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetPricingSettingsQuery(), cancellationToken));
    }

    [HttpPut]
    [SwaggerOperation(Summary = "Update the pricing-calculator settings")]
    [SwaggerResponse(204, "Settings updated")]
    [SwaggerResponse(400, "Validation failed")]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdatePricingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePricingSettingsCommand(
            request.BufferRate, request.StripePercent, request.StripeFixedFee, request.MinimumFloor);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }
}
