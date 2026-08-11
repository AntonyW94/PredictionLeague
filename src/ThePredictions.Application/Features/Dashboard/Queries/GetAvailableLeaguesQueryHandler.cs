using MediatR;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The leagues a player is being offered: still open, not one they are already in, and in a season they hold a pass for.
/// </summary>
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
                league.HasEntryCode))
            .ToList();
    }

    /// <summary>
    /// Whether a league should appear in the list.
    /// </summary>
    /// <remarks>
    /// Three things have to hold. It must be findable - a public league always is, and a private one only if its
    /// administrator has chosen to list it, because the point of a private league is that you have to be told about it. It
    /// must still be open. And the player must already hold a pass for its season: passes are bought first and leagues
    /// joined afterwards, so offering a league they cannot enter would be an invitation to a dead end.
    ///
    /// The deadline is safe to read as non-null in the projection above only because this rule has already rejected a
    /// league without one - which is what <c>LeagueEntry.IsOpen</c> exists to make explicit.
    /// </remarks>
    private static bool IsOnOffer(JoinableLeagueRow league, DateTime utcNow)
    {
        if (league.HasEntryCode && !league.IsListed)
            return false;

        if (!LeagueEntry.IsOpen(league.EntryDeadlineUtc, utcNow))
            return false;

        return league.HasSeasonPass;
    }
}
