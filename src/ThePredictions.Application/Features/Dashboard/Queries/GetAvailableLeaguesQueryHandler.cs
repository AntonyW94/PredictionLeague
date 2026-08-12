using MediatR;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The leagues a player is being offered: still open, and not one they are already in.
/// </summary>
/// <remarks>
/// A season pass is <b>not</b> required to be shown a league. It used to be, and hiding them turned out to confuse people
/// into thinking there was nothing to join: a pass is bought per season, and somebody without one saw an empty list rather
/// than a reason to buy. Each league now says whether a pass is still needed, and the gate itself stays where it belongs -
/// on the join, which refuses without one.
/// </remarks>
public class GetAvailableLeaguesQueryHandler(
    IJoinableLeaguesQuery joinableLeaguesQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetAvailableLeaguesQuery, IEnumerable<AvailableLeagueDto>>
{
    public async Task<IEnumerable<AvailableLeagueDto>> Handle(
        GetAvailableLeaguesQuery request,
        CancellationToken cancellationToken)
    {
        var leagues = await joinableLeaguesQuery.ExecuteAsync(request.UserId, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        return leagues
            .Where(league => IsOnOffer(league, utcNow))
            .OrderByDescending(league => league.SeasonStartDateUtc)
            .ThenBy(league => league.Name, StringComparer.InvariantCultureIgnoreCase)
            .Select(league => new AvailableLeagueDto(
                league.LeagueId,
                league.Name,
                league.SeasonName,
                league.Price,
                league.EntryDeadlineUtc!.Value,
                league.MemberCount,
                PrizeFund.Total(league.Price, league.MemberCount, league.PrizeFundOverride),
                league.HasEntryCode,
                RequiresSeasonPass: !league.HasSeasonPass))
            .ToList();
    }

    /// <summary>
    /// Whether a league should appear in the list.
    /// </summary>
    /// <remarks>
    /// Two things have to hold. It must be findable - a public league always is, and a private one only if its
    /// administrator has chosen to list it, because the point of a private league is that you have to be told about it. And
    /// it must still be open.
    ///
    /// Holding a season pass is deliberately not one of them; see the note on this class. A league needing one is still
    /// offered, marked as needing it.
    ///
    /// The deadline is safe to read as non-null in the projection above only because this rule has already rejected a
    /// league without one - which is what <c>LeagueEntry.IsOpen</c> exists to make explicit.
    /// </remarks>
    private static bool IsOnOffer(JoinableLeagueRow league, DateTime utcNow)
    {
        if (league.HasEntryCode && !league.IsListed)
            return false;

        return LeagueEntry.IsOpen(league.EntryDeadlineUtc, utcNow);
    }
}
