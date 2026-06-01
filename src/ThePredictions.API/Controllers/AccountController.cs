using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Account.Commands;
using ThePredictions.Application.Features.Account.Queries;
using ThePredictions.Contracts.Account;
using ThePredictions.Contracts.Payouts;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Account - Manage user profile and settings")]
public class AccountController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("details")]
    [SwaggerOperation(
        Summary = "Get current user's account details",
        Description = "Returns the authenticated user's profile information including name, email, and account settings.")]
    [SwaggerResponse(200, "User details retrieved successfully", typeof(UserDetails))]
    [SwaggerResponse(401, "Not authenticated - valid JWT required")]
    [SwaggerResponse(404, "User not found")]
    public async Task<ActionResult<UserDetails>> GetUserDetailsAsync(CancellationToken cancellationToken)
    {
        var query = new GetUserQuery(CurrentUserId);
        var userDetails = await mediator.Send(query, cancellationToken);

        if (userDetails == null)
            return NotFound();

        return Ok(userDetails);
    }

    [HttpPut("details")]
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
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPut("theme")]
    [SwaggerOperation(
        Summary = "Update theme preference",
        Description = "Sets the user's preferred theme to 'light' or 'dark'.")]
    [SwaggerResponse(204, "Theme preference updated successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> UpdateThemePreferenceAsync(
        [FromBody, SwaggerParameter("Theme name ('light' or 'dark')", Required = true)] string theme,
        CancellationToken cancellationToken)
    {
        var command = new UpdateThemePreferenceCommand(CurrentUserId, theme);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpGet("payout-details")]
    [SwaggerOperation(
        Summary = "Get the current user's payout details",
        Description = "Returns the user's own (decrypted) payout bank details and the list of prize-league admins who can see them.")]
    [SwaggerResponse(200, "Payout details retrieved", typeof(MyPayoutDetailsDto))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<MyPayoutDetailsDto>> GetPayoutDetailsAsync(CancellationToken cancellationToken)
    {
        var details = await mediator.Send(new GetMyPayoutDetailsQuery(CurrentUserId), cancellationToken);
        return Ok(details);
    }

    [HttpPut("payout-details")]
    [SwaggerOperation(
        Summary = "Save the current user's payout details",
        Description = "Stores the user's payout bank details (encrypted at rest). Used by league admins to pay prize winnings directly; the platform never moves money.")]
    [SwaggerResponse(204, "Payout details saved")]
    [SwaggerResponse(400, "Validation failed")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> SetPayoutDetailsAsync(
        [FromBody, SwaggerParameter("Payout bank details", Required = true)] SetPayoutDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SetPayoutDetailsCommand(CurrentUserId, request.AccountName, request.SortCode, request.AccountNumber);
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpDelete("payout-details")]
    [SwaggerOperation(
        Summary = "Delete the current user's payout details",
        Description = "Removes the user's stored payout bank details.")]
    [SwaggerResponse(204, "Payout details deleted")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> DeletePayoutDetailsAsync(CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePayoutDetailsCommand(CurrentUserId), cancellationToken);

        return NoContent();
    }
}
