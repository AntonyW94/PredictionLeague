using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrator")] // This entire controller is protected and only accessible by users in the "Administrator" role.
    public class AdminController : ControllerBase
    {
        // Example of an admin-only endpoint
        // POST: api/admin/gameweek/results
        [HttpPost("gameweek/results")]
        public IActionResult UpdateGameWeekResults()
        {
            // Here you would add logic to:
            // 1. Accept a list of match results from the request body.
            // 2. Call a service (e.g., IGameWeekService) to update the actual scores for each match.
            // 3. Trigger the points calculation for the entire gameweek.
            return Ok(new { message = "Admin action completed." });
        }
    }
}
