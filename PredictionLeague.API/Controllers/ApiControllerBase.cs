using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("User ID could not be found in the token.");
    protected string CurrentUserFirstName => User.FindFirstValue("FirstName") ?? string.Empty;
    protected string CurrentUserLastName => User.FindFirstValue("LastName") ?? string.Empty;
}