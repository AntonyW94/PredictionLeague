using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Predictions.Commands;
using PredictionLeague.Application.Features.Predictions.Queries;
using PredictionLeague.Contracts.Predictions;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Predictions - Submit and retrieve match predictions")]
public class PredictionsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public PredictionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{roundId:int}")]
    [SwaggerOperation(
        Summary = "Get prediction page data for a round",
        Description = "Returns all matches in the round with the current user's predictions (if any). Includes match details, deadlines, and current scores for in-progress matches.")]
    [SwaggerResponse(200, "Prediction page data retrieved successfully", typeof(PredictionPageDto))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<ActionResult<PredictionPageDto>> GetPredictionPageDataAsync(
        [SwaggerParameter("Round identifier")] int roundId,
        CancellationToken cancellationToken)
    {
        var query = new GetPredictionPageDataQuery(roundId, CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpPost("submit")]
    [SwaggerOperation(
        Summary = "Submit predictions for a round",
        Description = "Submits or updates the current user's score predictions for matches in a round. Predictions can be updated until the round deadline.")]
    [SwaggerResponse(204, "Predictions submitted successfully")]
    [SwaggerResponse(400, "Validation failed or deadline has passed")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(404, "Round not found")]
    public async Task<IActionResult> SubmitAsync(
        [FromBody, SwaggerParameter("Prediction data including round ID and match predictions", Required = true)] SubmitPredictionsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SubmitPredictionsCommand(
            CurrentUserId,
            request.RoundId,
            request.Predictions
        );

        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
}
