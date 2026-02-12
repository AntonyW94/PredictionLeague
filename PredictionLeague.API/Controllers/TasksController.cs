using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PredictionLeague.API.Filters;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;
using PredictionLeague.Application.Features.Admin.Tasks.Commands;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[ApiKeyAuthorise]
[DisableRateLimiting]
[SwaggerTag("Scheduled Tasks - Automated background jobs (API key required)")]
public class TasksController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("score-update")]
    [SwaggerOperation(
        Summary = "Trigger live score update",
        Description = "Fetches latest scores from the external football API and updates in-progress matches. Called every minute by cron job.")]
    [SwaggerResponse(204, "Scores updated successfully")]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> TriggerLiveScoreUpdateAsync(CancellationToken cancellationToken)
    {
        var command = new UpdateAllLiveScoresCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("sync")]
    [SwaggerOperation(
        Summary = "Sync season data",
        Description = "Synchronises all active seasons with the external football API. Updates match schedules, team data, and round information. Called daily.")]
    [SwaggerResponse(204, "Season data synced successfully")]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> SyncSeasonsAsync(CancellationToken cancellationToken)
    {
        var command = new SyncAllActiveSeasonsCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("send-reminders")]
    [SwaggerOperation(
        Summary = "Send prediction reminders",
        Description = "Sends email reminders to users who haven't submitted predictions for upcoming round deadlines. Called every 30 minutes.")]
    [SwaggerResponse(204, "Reminders sent successfully")]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> SendScheduledRemindersAsync(CancellationToken cancellationToken)
    {
        var command = new SendScheduledRemindersCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("publish-upcoming-rounds")]
    [SwaggerOperation(
        Summary = "Publish upcoming rounds",
        Description = "Automatically publishes draft rounds that are ready to be made visible to users. Called daily.")]
    [SwaggerResponse(204, "Rounds published successfully")]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> PublishUpcomingRoundsAsync(CancellationToken cancellationToken)
    {
        var command = new PublishUpcomingRoundsCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("recalculate-season-stats/{seasonId:int}")]
    [SwaggerOperation(
        Summary = "Recalculate season statistics",
        Description = "Recalculates all leaderboards, points, and statistics for the specified season. Use after manual score corrections.")]
    [SwaggerResponse(204, "Statistics recalculated successfully")]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> RecalculateSeasonStatsAsync(
        [SwaggerParameter("Season identifier")] int seasonId,
        CancellationToken cancellationToken)
    {
        var command = new RecalculateSeasonStatsCommand(seasonId);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("cleanup")]
    [SwaggerOperation(
        Summary = "Clean up expired data",
        Description = "Deletes expired and old data including password reset tokens older than 30 days. Called daily.")]
    [SwaggerResponse(200, "Cleanup completed", typeof(CleanupResult))]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> CleanupExpiredDataAsync(CancellationToken cancellationToken)
    {
        var command = new CleanupExpiredDataCommand();
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }
}
