using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.RunningCosts.Commands;
using ThePredictions.Application.Features.Admin.RunningCosts.Queries;
using ThePredictions.Contracts.Admin.RunningCosts;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Running Costs - record website running costs for the pricing calculator (Admin only)")]
public class RunningCostsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "List running costs")]
    [SwaggerResponse(200, "Running costs returned", typeof(IEnumerable<RunningCostDto>))]
    public async Task<ActionResult<IEnumerable<RunningCostDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetRunningCostsQuery(), cancellationToken));
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create a running cost")]
    [SwaggerResponse(204, "Running cost created")]
    [SwaggerResponse(400, "Validation failed")]
    public async Task<IActionResult> CreateAsync(
        [FromBody] SaveRunningCostRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRunningCostCommand(
            request.Name, request.Amount, request.Frequency, request.StartDateUtc, request.EndDateUtc, request.Payer, request.Notes);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Update a running cost")]
    [SwaggerResponse(204, "Running cost updated")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(404, "Running cost not found")]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] SaveRunningCostRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRunningCostCommand(
            id, request.Name, request.Amount, request.Frequency, request.StartDateUtc, request.EndDateUtc, request.Payer, request.Notes);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "Delete a running cost")]
    [SwaggerResponse(204, "Running cost deleted")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteRunningCostCommand(id), cancellationToken);

        return NoContent();
    }
}
