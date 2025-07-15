using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Predictions;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictionService;
    private readonly IValidator<SubmitPredictionsRequest> _validator;

    public PredictionsController(IPredictionService predictionService, IValidator<SubmitPredictionsRequest> validator)
    {
        _predictionService = predictionService;
        _validator = validator;
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
        var validationResult = await _validator.ValidateAsync(request);

        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors);

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized("User ID could not be found in the token.");

            await _predictionService.SubmitPredictionsAsync(userId, request);

            return Ok(new { message = "Predictions submitted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}