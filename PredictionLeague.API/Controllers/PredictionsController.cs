using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.API.Contracts;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Services;
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

        // POST: api/predictions
        [HttpPost]
        public async Task<IActionResult> SubmitPredictions([FromBody] SubmitPredictionsRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                // Map the DTO from the request to the Core domain model.
                var predictionsToSubmit = request.Predictions.Select(p => new UserPrediction
                {
                    MatchId = p.MatchId,
                    PredictedHomeScore = p.PredictedHomeScore,
                    PredictedAwayScore = p.PredictedAwayScore
                });

                await _predictionService.SubmitPredictionsAsync(userId, request.GameWeekId, predictionsToSubmit);
                return Ok(new { message = "Predictions submitted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
