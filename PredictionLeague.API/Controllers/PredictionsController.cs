using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;
using PredictionLeague.Shared.Predictions;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

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

    [HttpGet("{roundId:int}")]
    public async Task<IActionResult> GetPredictionPageData(int roundId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ok(await _predictionService.GetPredictionPageDataAsync(roundId, userId!));
    }

    [HttpPost]
    public async Task<IActionResult> SubmitPredictions([FromBody] SubmitPredictionsRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized("User ID could not be found in the token.");

        await _predictionService.SubmitPredictionsAsync(userId, request);

        return Ok(new { message = "Predictions submitted successfully." });
    }
}