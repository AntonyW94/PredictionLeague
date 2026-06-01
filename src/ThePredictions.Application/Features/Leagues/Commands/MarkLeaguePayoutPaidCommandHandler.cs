using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Leagues.Commands;

public class MarkLeaguePayoutPaidCommandHandler(
    ILeagueRepository leagueRepository,
    IRoundRepository roundRepository,
    IWinningsRepository winningsRepository,
    ILeaguePayoutRepository leaguePayoutRepository,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<MarkLeaguePayoutPaidCommand>
{
    public async Task Handle(MarkLeaguePayoutPaidCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.WinnerUserId);

        var league = await leagueRepository.GetByIdAsync(request.LeagueId, cancellationToken);
        Guard.Against.EntityNotFound(request.LeagueId, league, "League");

        if (league!.AdministratorUserId != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the league administrator can mark payouts as paid.");

        var rounds = await roundRepository.GetAllForSeasonAsync(league.SeasonId, cancellationToken);
        var seasonComplete = rounds.Count > 0 && rounds.Values.All(r => r.Status == RoundStatus.Completed);
        if (!seasonComplete)
            throw new InvalidOperationException("Payouts cannot be marked as paid until the season is complete.");

        var payout = await leaguePayoutRepository.GetByLeagueAndUserAsync(request.LeagueId, request.WinnerUserId, cancellationToken);

        if (payout is null)
        {
            var total = await winningsRepository.GetUserLeagueTotalAsync(request.LeagueId, request.WinnerUserId, cancellationToken);
            if (total <= 0)
                throw new InvalidOperationException("This player has no winnings to pay out in this league.");

            payout = LeaguePayout.Create(request.LeagueId, request.WinnerUserId, total, dateTimeProvider);
        }

        payout.MarkPaid(dateTimeProvider);

        await leaguePayoutRepository.UpsertAsync(payout, cancellationToken);
    }
}
