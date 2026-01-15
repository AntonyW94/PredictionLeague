using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Admin.Users.Commands;
using PredictionLeague.Application.Features.Admin.Users.Queries;
using PredictionLeague.Contracts;
using PredictionLeague.Domain.Common.Constants;
using PredictionLeague.Contracts.Admin.Users;

namespace PredictionLeague.API.Controllers.Admin;

[Authorize(Roles = RoleNames.Administrator)]
[ApiController]
[Route("api/admin/[controller]")]
public class UsersController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    #region Read

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var query = new GetAllUsersQuery();
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{userId}/owns-leagues")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> UserOwnsLeaguesAsync(string userId, CancellationToken cancellationToken)
    {
        var query = new UserOwnsLeaguesQuery(userId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    #endregion

    #region Update

    [HttpPost("{userId}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateRoleAsync(string userId, [FromBody] string newRole, CancellationToken cancellationToken)
    {
        var command = new UpdateUserRoleCommand(userId, newRole);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Delete

    [HttpPost("{userId}/delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAsync(string userId, [FromBody] DeleteUserRequest request, CancellationToken cancellationToken)
    {
        var isDeletingUserAdmin = User.IsInRole(RoleNames.Administrator);

        var command = new DeleteUserCommand(
            userId,
            CurrentUserId,
            isDeletingUserAdmin,
            request.NewAdministratorId
        );

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    #endregion
}