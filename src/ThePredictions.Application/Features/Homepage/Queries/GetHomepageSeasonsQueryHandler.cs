using MediatR;
using ThePredictions.Contracts.Homepage;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Homepage.Queries;

/// <summary>
/// The seasons the public homepage advertises: what is on now, what is coming, and how much is at stake.
/// </summary>
public class GetHomepageSeasonsQueryHandler(
    IHomepageSeasonsQuery homepageSeasonsQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetHomepageSeasonsQuery, IEnumerable<HomepageSeasonDto>>
{
    public async Task<IEnumerable<HomepageSeasonDto>> Handle(
        GetHomepageSeasonsQuery request,
        CancellationToken cancellationToken)
    {
        // Read the clock once. Whether a season is shown, whether it is under way and whether it is still to come were three
        // separate GETUTCDATE() calls, so a season could in principle have been described as neither.
        var utcNow = dateTimeProvider.UtcNow;

        var data = await homepageSeasonsQuery.ExecuteAsync(cancellationToken);

        return data.Seasons
            .Where(season => HasNotFinished(season, utcNow))
            .OrderBy(season => season.StartDateUtc)
            .Select(season => ToDto(season, data, utcNow))
            .ToList();
    }

    /// <summary>
    /// Whether the season still belongs on the homepage.
    /// </summary>
    /// <remarks>
    /// Until its end date has passed, inclusive - a season ending today is still this season. The homepage is an advert, and a
    /// finished competition advertises nothing.
    /// </remarks>
    private static bool HasNotFinished(HomepageSeasonRow season, DateTime utcNow) => season.EndDateUtc >= utcNow;

    private static HomepageSeasonDto ToDto(HomepageSeasonRow season, HomepageSeasonsData data, DateTime utcNow)
    {
        var leagues = data.Leagues.Where(league => league.SeasonId == season.Id).ToList();

        return new HomepageSeasonDto(
            season.Id,
            season.Name,
            season.CompetitionType,
            season.StartDateUtc,
            season.EndDateUtc,
            IsInProgress(season, utcNow),
            IsUpcoming(season, utcNow),
            leagues.Count,
            PlayerCount(season.Id, data),
            TotalPrizeFund(leagues));
    }

    /// <summary>Under way: started and not yet finished, both bounds inclusive.</summary>
    private static bool IsInProgress(HomepageSeasonRow season, DateTime utcNow) =>
        season.StartDateUtc <= utcNow && utcNow <= season.EndDateUtc;

    /// <summary>Still to come: has not started yet.</summary>
    private static bool IsUpcoming(HomepageSeasonRow season, DateTime utcNow) => season.StartDateUtc > utcNow;

    /// <summary>
    /// How many people are playing in the season, counting somebody in three of its leagues once.
    /// </summary>
    private static int PlayerCount(int seasonId, HomepageSeasonsData data) =>
        data.Memberships
            .Where(membership => membership.SeasonId == seasonId)
            .Select(membership => membership.UserId)
            .Distinct()
            .Count();

    /// <summary>
    /// Everything at stake across the season's leagues, through the same rule a single league's page uses.
    /// </summary>
    /// <remarks>
    /// Third place this formula has been found written out in SQL, after the My Leagues tile and the available-leagues list.
    /// </remarks>
    private static decimal TotalPrizeFund(IEnumerable<HomepageLeagueRow> leagues) =>
        leagues.Sum(league => PrizeFund.Total(league.Price, league.ApprovedMemberCount, league.PrizeFundOverride));
}
