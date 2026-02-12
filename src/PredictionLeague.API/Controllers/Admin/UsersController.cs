using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Users.Commands;
using PredictionLeague.Application.Features.Admin.Users.Queries;
using PredictionLeague.Contracts.Admin.Users;
using PredictionLeague.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Users - Manage user accounts and roles (Admin only)")]
public class UsersController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

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
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{userId}/owns-leagues")]
    [SwaggerOperation(
        Summary = "Check if user owns leagues",
        Description = "Returns whether the user is an administrator of any leagues. Used to determine if league ownership must be transferred before account deletion.")]
    [SwaggerResponse(200, "Returns boolean indicating league ownership", typeof(bool))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<bool>> UserOwnsLeaguesAsync(
        [SwaggerParameter("User identifier")] string userId,
        CancellationToken cancellationToken)
    {
        var query = new UserOwnsLeaguesQuery(userId);
        return Ok(await _mediator.Send(query, cancellationToken));
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
        await _mediator.Send(command, cancellationToken);
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

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    #endregion
}
