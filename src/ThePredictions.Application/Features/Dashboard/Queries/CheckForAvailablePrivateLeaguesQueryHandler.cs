using MediatR;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Whether there is any private league the player could join, which is what decides if the dashboard offers them somewhere
/// to type an entry code.
/// </summary>
/// <remarks>
/// Deliberately a looser question than <see cref="GetAvailableLeaguesQueryHandler"/> asks, and the difference is worth
/// knowing: this counts a private league whether or not its administrator has listed it, because somebody who has been
/// given a code should be able to use it. It also does <b>not</b> require a season pass, so the prompt can appear for a
/// league the player could not yet enter. Both were true of the old statement and both are preserved - the second is
/// recorded in the plan document, because it looks more like an oversight than a decision.
/// </remarks>
public class CheckForAvailablePrivateLeaguesQueryHandler(
    IJoinableLeaguesQuery joinableLeaguesQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<CheckForAvailablePrivateLeaguesQuery, bool>
{
    public async Task<bool> Handle(
        CheckForAvailablePrivateLeaguesQuery request,
        CancellationToken cancellationToken)
    {
        var leagues = await joinableLeaguesQuery.ExecuteAsync(request.UserId, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        return leagues.Any(league => league.HasEntryCode && LeagueEntry.IsOpen(league.EntryDeadlineUtc, utcNow));
    }
}
