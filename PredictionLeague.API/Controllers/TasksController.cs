using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.API.Filters;
using PredictionLeague.Application.Features.Admin.Rounds.Commands;
using PredictionLeague.Application.Features.Admin.Seasons.Commands;

namespace PredictionLeague.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiKeyAuthorise]
    public class TasksController : ApiControllerBase
    {
        private readonly IMediator _mediator;

        public TasksController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [AllowAnonymous]
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

        [AllowAnonymous]
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

        [AllowAnonymous]
        [HttpPost("send-reminders")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SendScheduledReminders(CancellationToken cancellationToken)
        {
            var command = new SendScheduledRemindersCommand();
            await _mediator.Send(command, cancellationToken);
            
            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("publish-upcoming-rounds")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> PublishUpcomingRoundsAsync(CancellationToken cancellationToken)
        {
            var command = new PublishUpcomingRoundsCommand();
            await _mediator.Send(command, cancellationToken);

            return NoContent();
        }
    }
}
