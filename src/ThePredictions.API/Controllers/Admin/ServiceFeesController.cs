using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.ServiceFees.Commands;
using ThePredictions.Application.Features.Admin.ServiceFees.Queries;
using ThePredictions.Contracts.Admin.ServiceFees;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Service Fees - per-transaction fees charged by providers (Stripe, SMS, email)")]
public class ServiceFeesController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "List provider service fees")]
    [SwaggerResponse(200, "Service fees returned", typeof(IEnumerable<ServiceFeeDto>))]
    public async Task<ActionResult<IEnumerable<ServiceFeeDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetServiceFeesQuery(), cancellationToken));
    }

    [HttpPut("{provider}")]
    [SwaggerOperation(Summary = "Update a provider's service fee")]
    [SwaggerResponse(204, "Service fee updated")]
    [SwaggerResponse(400, "Validation failed")]
    public async Task<IActionResult> UpdateAsync(
        ServiceFeeProvider provider,
        [FromBody] UpdateServiceFeeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateServiceFeeCommand(provider, request.PercentFee, request.FixedFee);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }
}
