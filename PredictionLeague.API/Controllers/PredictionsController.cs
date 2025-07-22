using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Predictions.Commands;
using PredictionLeague.Application.Features.Predictions.Queries;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PredictionsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public PredictionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{roundId:int}")]
    public async Task<IActionResult> GetPredictionPageDataAsync(int roundId)
    {
        var query = new GetPredictionPageDataQuery(roundId, CurrentUserId);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAsync([FromBody] SubmitPredictionsRequest request)
    {
        try
        {
            var command = new SubmitPredictionsCommand(CurrentUserId, request.RoundId, request.Predictions);
            await _mediator.Send(command);

            return Ok(new { message = "Predictions submitted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}