using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The league management screen: the public leagues, the private ones this player runs, and - for an administrator - everybody
/// else's private ones.
/// </summary>
public class GetManageLeaguesQueryHandler(IManageLeaguesQuery manageLeaguesQuery)
    : IRequestHandler<GetManageLeaguesQuery, ManageLeaguesDto>
{
    /// <summary>What the screen shows in place of an entry code for a league that does not have one.</summary>
    /// <remarks>
    /// The statement this replaces produced this word with <c>ISNULL(l.[EntryCode], 'Public')</c>, so a sentinel travelled to the
    /// browser in a field named for a code. It is a label, it belongs on this side, and naming it says so.
    /// </remarks>
    private const string PublicLeagueLabel = "Public";

    public async Task<ManageLeaguesDto> Handle(GetManageLeaguesQuery request, CancellationToken cancellationToken)
    {
        var leagues = await manageLeaguesQuery.ExecuteAsync(cancellationToken);

        var ordered = NewestSeasonFirst(leagues).ToList();

        return new ManageLeaguesDto
        {
            // An ordinary player sees only the private leagues they run. The other two lists are an administrator's view of
            // everything on the site, which is why they stay empty otherwise rather than being filtered by the read.
            PublicLeagues = request.IsAdmin ? ToDtos(ordered.Where(IsPublic)) : [],
            MyPrivateLeagues = ToDtos(ordered.Where(league => IsPrivateRunBy(league, request.UserId))),
            OtherPrivateLeagues = request.IsAdmin
                ? ToDtos(ordered.Where(league => IsPrivateNotRunBy(league, request.UserId)))
                : []
        };
    }

    /// <summary>
    /// Newest season first, and alphabetically within it.
    /// </summary>
    /// <remarks>
    /// By the season's start date rather than its name, so "2026/27" sorts after "2025/26" without depending on how a season
    /// happens to be named.
    /// </remarks>
    private static IEnumerable<ManageLeagueRow> NewestSeasonFirst(IEnumerable<ManageLeagueRow> leagues) =>
        leagues
            .OrderByDescending(league => league.SeasonStartDateUtc)
            .ThenBy(league => league.Name, StringComparer.InvariantCultureIgnoreCase);

    /// <summary>A league anybody can join, which is one with no entry code.</summary>
    private static bool IsPublic(ManageLeagueRow league) => league.EntryCode is null;

    /// <summary>A private league this player administers.</summary>
    private static bool IsPrivateRunBy(ManageLeagueRow league, string userId) =>
        !IsPublic(league) && league.AdministratorUserId == userId;

    /// <summary>A private league somebody else administers.</summary>
    private static bool IsPrivateNotRunBy(ManageLeagueRow league, string userId) =>
        !IsPublic(league) && league.AdministratorUserId != userId;

    private static List<LeagueDto> ToDtos(IEnumerable<ManageLeagueRow> leagues) => leagues.Select(ToDto).ToList();

    private static LeagueDto ToDto(ManageLeagueRow league) =>
        new(league.Id,
            league.Name,
            league.SeasonName,
            league.MemberCount,
            league.Price,
            league.EntryCode ?? PublicLeagueLabel,
            league.EntryDeadlineUtc,
            league.PointsForExactScore,
            league.PointsForCorrectResult);
}
