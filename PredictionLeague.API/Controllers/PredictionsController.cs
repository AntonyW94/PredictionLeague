using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Predictions.Commands;
using PredictionLeague.Application.Features.Predictions.Queries;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PredictionsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public PredictionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{roundId:int}")]
    [ProducesResponseType(typeof(IEnumerable<PredictionPageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PredictionPageDto>> GetPredictionPageDataAsync(int roundId, CancellationToken cancellationToken)
    {
        var query = new GetPredictionPageDataQuery(roundId, CurrentUserId);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpPost("submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitAsync([FromBody] SubmitPredictionsRequest request, CancellationToken cancellationToken)
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