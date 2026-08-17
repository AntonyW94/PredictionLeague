using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.Users.Commands;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Users - Manage user accounts and roles (Admin only)")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class UsersController(IMediator mediator) : ApiControllerBase
{
    #region Read

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all users",
        Description = "Returns all registered users with their profile information and roles.")]
    [SwaggerResponse(200, "Users retrieved successfully", typeof(IEnumerable<UserDto>))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var query = new GetAllUsersQuery();
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{userId}/deletion-impact")]
    [SwaggerOperation(
        Summary = "Count what deleting a user would destroy",
        Description = "Returns how many of each kind of record the account holds - season passes, memberships, predictions, winnings, payouts and the rest - so the confirmation dialog can state the consequences before the administrator commits. Also reports how many leagues the account administers, which must be re-assigned rather than deleted.")]
    [SwaggerResponse(200, "Deletion impact retrieved successfully", typeof(UserDeletionImpactDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "User not found")]
    public async Task<ActionResult<UserDeletionImpactDto>> DeletionImpactAsync(
        [SwaggerParameter("User identifier")] string userId,
        CancellationToken cancellationToken)
    {
        var query = new GetUserDeletionImpactQuery(userId);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    #endregion

    #region Update

    [HttpPost("{userId}/role")]
    [SwaggerOperation(
        Summary = "Update user role",
        Description = "Changes a user's role (e.g., promote to Administrator or demote to User).")]
    [SwaggerResponse(204, "Role updated successfully")]
    [SwaggerResponse(400, "Invalid role specified")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> UpdateRoleAsync(
        [SwaggerParameter("User identifier")] string userId,
        [FromBody, SwaggerParameter("New role assignment", Required = true)] UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserRoleCommand(userId, request.NewRole);
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Delete

    [HttpPost("{userId}/delete")]
    [SwaggerOperation(
        Summary = "Delete user account",
        Description = "Permanently deletes a user account. If the user owns leagues, a new administrator must be specified to take over ownership.")]
    [SwaggerResponse(204, "User deleted successfully")]
    [SwaggerResponse(400, "User owns leagues and no new administrator specified")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    [SwaggerResponse(404, "User not found")]
    public async Task<IActionResult> DeleteAsync(
        [SwaggerParameter("User identifier")] string userId,
        [FromBody, SwaggerParameter("Deletion options including optional new league administrator", Required = true)] DeleteUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DeleteUserCommand(
            userId,
            CurrentUserId,
            request.NewAdministratorId
        );

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    #endregion
}
