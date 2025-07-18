using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Account.Commands;
using PredictionLeague.Application.Features.Account.Queries;
using PredictionLeague.Contracts.Account;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]/details")]
[Authorize]
public class AccountController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserDetails()
    {
        var query = new GetUserQuery(CurrentUserId);
        var userDetails = await _mediator.Send(query);

        return userDetails == null ? NotFound("User not found.") : Ok(userDetails);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUserDetails([FromBody] UpdateUserDetailsRequest request)
    {
        var command = new UpdateUserDetailsCommand(request, CurrentUserId);
        await _mediator.Send(command);

        return Ok(new { message = "Details updated successfully." });
    }
}