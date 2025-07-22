using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Account.Commands;
using PredictionLeague.Application.Features.Account.Queries;
using PredictionLeague.Contracts.Account;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    public async Task<ActionResult<UserDetails>> GetUserDetailsAsync()
    {
        var query = new GetUserQuery(CurrentUserId);
        var userDetails = await _mediator.Send(query);
      
        if (userDetails == null)
            return NotFound(); 

        return Ok(userDetails);
    }

    [HttpPut("details")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserDetailsAsync([FromBody] UpdateUserDetailsRequest request)
    {
        var command = new UpdateUserDetailsCommand(CurrentUserId, request.FirstName, request.LastName, request.PhoneNumber);
        await _mediator.Send(command);

        return NoContent();
    }
}