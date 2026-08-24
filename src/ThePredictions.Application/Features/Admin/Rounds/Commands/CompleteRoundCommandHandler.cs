using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Features.Badges.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <inheritdoc cref="CompleteRoundCommand"/>
public class CompleteRoundCommandHandler(
    IMediator mediator,
    ILeagueRepository leagueRepository,
    IRoundRepository roundRepository) : IRequestHandler<CompleteRoundCommand>
{
    /// <summary>
    /// Settles prizes, awards badges, then sends the results digest and the prize emails - in that order,
    /// so winners see "here's how you did" before "and you won!". Every step is idempotent, so re-completing
    /// the round neither double-awards nor re-sends.
    /// </summary>
    public async Task Handle(CompleteRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        Guard.Against.EntityNotFound(request.RoundId, round, "Round");

        var leagueIds = await leagueRepository.GetLeagueIdsForSeasonAsync(round.SeasonId, cancellationToken);

        foreach (var leagueId in leagueIds)
        {
            await mediator.Send(new ProcessPrizesCommand { RoundId = round.Id, LeagueId = leagueId }, cancellationToken);
        }

        var badgesAwarded = await mediator.Send(new EvaluateBadgesForRoundCommand(round.Id), cancellationToken);

        await mediator.Send(new SendRoundDigestEmailsCommand(round.Id, BadgesAwarded: badgesAwarded), cancellationToken);
        await mediator.Send(new SendPrizeNotificationsCommand(round.Id), cancellationToken);
    }
}
