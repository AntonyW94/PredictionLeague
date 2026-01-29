using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Account.Commands;
using PredictionLeague.Application.Features.Account.Queries;
using PredictionLeague.Contracts.Account;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Account - Manage user profile and settings")]
public class AccountController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("details")]
    [ProducesResponseType(typeof(UserDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get current user's account details",
        Description = "Returns the authenticated user's profile information including name, email, and account settings.")]
    [SwaggerResponse(200, "User details retrieved successfully", typeof(UserDetails))]
    [SwaggerResponse(401, "Not authenticated - valid JWT required")]
    [SwaggerResponse(404, "User not found")]
    public async Task<ActionResult<UserDetails>> GetUserDetailsAsync(CancellationToken cancellationToken)
    {
        var query = new GetUserQuery(CurrentUserId);
        var userDetails = await _mediator.Send(query, cancellationToken);

        if (userDetails == null)
            return NotFound();

        return Ok(userDetails);
    }

    [HttpPut("details")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Update current user's account details",
        Description = "Updates the authenticated user's profile information. Only provided fields are updated.")]
    [SwaggerResponse(204, "User details updated successfully")]
    [SwaggerResponse(400, "Validation failed - check error details")]
    [SwaggerResponse(401, "Not authenticated - valid JWT required")]
    public async Task<IActionResult> UpdateUserDetailsAsync(
        [FromBody, SwaggerParameter("Updated profile information", Required = true)] UpdateUserDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserDetailsCommand(CurrentUserId, request.FirstName, request.LastName, request.PhoneNumber);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
}
