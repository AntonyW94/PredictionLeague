using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PredictionLeague.API.Filters;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;

namespace PredictionLeague.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[ApiKeyAuthorise]
[DisableRateLimiting]
public class TasksController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("score-update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TriggerLiveScoreUpdate(CancellationToken cancellationToken)
    {
        var command = new UpdateAllLiveScoresCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncSeasonsAsync(CancellationToken cancellationToken)
    {
        var command = new SyncAllActiveSeasonsCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("send-reminders")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendScheduledReminders(CancellationToken cancellationToken)
    {
        var command = new SendScheduledRemindersCommand();
        await _mediator.Send(command, cancellationToken);
            
        return NoContent();
    }

    [HttpPost("publish-upcoming-rounds")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PublishUpcomingRoundsAsync(CancellationToken cancellationToken)
    {
        var command = new PublishUpcomingRoundsCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
    
    [HttpPost("recalculate-season-stats/{seasonId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecalculateSeasonStats(int seasonId, CancellationToken cancellationToken)
    {
        var command = new RecalculateSeasonStatsCommand(seasonId);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
}