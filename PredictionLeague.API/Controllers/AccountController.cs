using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Domain.Models;
using PredictionLeague.Shared.Account;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]/details")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserDetails()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized("User ID could not be found in the token.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");
        
        if (user.Email == null)
            return NotFound("Email not found for the user.");

        var userDetails = new UserDetails
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        };

        return Ok(userDetails);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUserDetails([FromBody] UpdateUserDetailsRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized("User ID could not be found in the token.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        if (!ModelState.IsValid)
        {
            var errorMessage = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "An unknown validation error occurred.";

            return BadRequest(errorMessage);
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
            return Ok(new { message = "Details updated successfully." }); 
        
        return BadRequest(result.Errors);
    }
}