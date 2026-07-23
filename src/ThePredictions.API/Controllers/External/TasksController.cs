using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ThePredictions.API.Filters;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Application.Features.Badges.Commands;
using ThePredictions.Application.Features.External.Tasks.Commands;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.External;

[Route("api/external/[controller]")]
[Route("api/tasks")]
[ApiController]
[ApiKeyAuthorise]
[DisableRateLimiting]
[SwaggerTag("Scheduled Tasks - Automated background jobs (API key required)")]
public class TasksController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("score-update")]
    [SwaggerOperation(
        Summary = "Trigger live score update",
        Description = "Fetches latest scores from the external football API and updates in-progress matches. Called every minute by cron job.")]
    [SwaggerResponse(204, "Scores updated successfully")]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> TriggerLiveScoreUpdateAsync(CancellationToken cancellationToken)
    {
        var command = new UpdateAllLiveScoresCommand();
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("backfill-badges")]
    [SwaggerOperation(
        Summary = "Backfill achievement badges",
        Description = "One-off replay of the badge evaluator over every completed round in chronological order, awarding existing players' historical badges with backdated achievement dates. Idempotent - safe to re-run.")]
    [SwaggerResponse(204, "Badges backfilled successfully")]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> BackfillBadgesAsync(CancellationToken cancellationToken)
    {
        await mediator.Send(new BackfillBadgesCommand(), cancellationToken);

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
        await mediator.Send(command, cancellationToken);

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
        await mediator.Send(command, cancellationToken);

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
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("freeze-prizes")]
    [SwaggerOperation(
        Summary = "Freeze due prize schemes",
        Description = "Freezes the prize scheme into confirmed prize settings for leagues whose entry deadline has passed. Called hourly.")]
    [SwaggerResponse(200, "Freeze completed", typeof(FreezeDuePrizeSchemesResult))]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> FreezeDuePrizeSchemesAsync(CancellationToken cancellationToken)
    {
        var command = new FreezeDuePrizeSchemesCommand();
        var result = await mediator.Send(command, cancellationToken);

        return Ok(result);
    }

    [HttpPost("send-welcome-emails")]
    [SwaggerOperation(
        Summary = "Send league welcome emails",
        Description = "Sends the one-off league welcome email to approved members of leagues whose entry deadline passed within the last 7 days (skipping leagues whose prizes are not yet frozen). Called hourly, after freeze-prizes.")]
    [SwaggerResponse(200, "Welcome emails processed", typeof(SendLeagueWelcomeEmailsResult))]
    [SwaggerResponse(401, "Invalid or missing API key")]
    public async Task<IActionResult> SendLeagueWelcomeEmailsAsync(CancellationToken cancellationToken)
    {
        var command = new SendLeagueWelcomeEmailsCommand();
        var result = await mediator.Send(command, cancellationToken);

        return Ok(result);
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
        await mediator.Send(command, cancellationToken);

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
        var result = await mediator.Send(command, cancellationToken);

        return Ok(result);
    }
}
