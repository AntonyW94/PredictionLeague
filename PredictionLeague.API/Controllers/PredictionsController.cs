﻿using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Predictions.Commands;
using PredictionLeague.Application.Features.Predictions.Queries;
using PredictionLeague.Contracts.Predictions;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PredictionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PredictionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{roundId:int}")]
    public async Task<IActionResult> GetPredictionPageDataAsync(int roundId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var query = new GetPredictionPageDataQuery(roundId, userId);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitAsync([FromBody] SubmitPredictionsRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized("User ID could not be found in the token.");

            var command = new SubmitPredictionsCommand(request, userId);
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