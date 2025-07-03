using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Core.Services;
using PredictionLeague.Shared.Predictions;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PredictionsController : ControllerBase
    {
        private readonly IPredictionService _predictionService;

        public PredictionsController(IPredictionService predictionService)
        {
            _predictionService = predictionService;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitPredictions([FromBody] SubmitPredictionsRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                await _predictionService.SubmitPredictionsAsync(userId, request);
                return Ok(new { message = "Predictions submitted successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                // Generic error for unexpected issues
                return StatusCode(500, "An unexpected error occurred.");
            }
        }
    }
}
