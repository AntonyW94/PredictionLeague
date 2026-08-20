using MediatR;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Features.Onboarding;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// The administrator's list of every account, with what each one has joined, held, earned and spent.
/// </summary>
/// <remarks>
/// The screen this feeds used to answer "does this account have a Season Pass" with a single yes or no, which was yes for
/// an account whose only passes were for seasons that finished a year ago. Three questions replace it, and answering them
/// needs to know which seasons have finished - so the seasons arrive with their round counts and
/// <see cref="SeasonCompletion"/> settles it, the same way the dashboards and the payouts screen do.
///
/// Every figure on the card is also a way in to the rows behind it, so the reply carries those rows. They were being read
/// in full and then collapsed into totals anyway; keeping them costs the network a few hundred rows across around 45
/// accounts and saves the client a request per popup.
/// </remarks>
public class GetAllUsersQueryHandler(IAdminUsersQuery adminUsersQuery)
    : IRequestHandler<GetAllUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var data = await adminUsersQuery.ExecuteAsync(cancellationToken);
        var seasons = data.Seasons.ToDictionary(season => season.SeasonId, SeasonFacts.From);
        var payoutDetailUserIds = data.UserIdsWithPayoutDetails.ToHashSet(StringComparer.Ordinal);

        return data.Users
            .Select(user => ToDto(user, data, seasons, payoutDetailUserIds))
            .OrderBy(user => user.FullName, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    private static UserDto ToDto(
        AdminUserRow user,
        AdminUsersData data,
        IReadOnlyDictionary<int, SeasonFacts> seasons,
        IReadOnlySet<string> payoutDetailUserIds)
    {
        var leagues = data.Leagues.Where(league => league.UserId == user.Id).ToList();
        var passRows = data.SeasonPasses.Where(pass => pass.UserId == user.Id).ToList();

        var memberships = Memberships(leagues, seasons);
        var administeredLeagues = AdministeredLeagues(leagues, seasons);
        var passes = Passes(passRows, seasons);
        var prizes = Prizes(data.Winnings.Where(winning => winning.UserId == user.Id), seasons);

        var hasPayoutDetails = payoutDetailUserIds.Contains(user.Id);
        var leaguesJoinedApproved = memberships.Count(membership => membership.Status == LeagueMemberStatus.Approved);

        return new UserDto(
            user.Id,
            // The full name, not the abbreviated one players see: this screen is for telling accounts apart.
            PlayerDisplayName.FormatFull(user.FirstName, user.LastName),
            user.Email,
            user.PhoneNumber,
            user.CreatedAtUtc,
            user.IsAdmin,
            user.HasPassword,
            data.LoginProviders
                .Where(provider => provider.UserId == user.Id)
                .Select(provider => provider.LoginProvider)
                .ToList(),
            user.EmailConfirmed,
            user.TermsAcceptedAtUtc is not null,
            user.MarketingOptInAtUtc is not null,
            hasPayoutDetails,
            passes.Count > 0,
            passes.Any(pass => pass.IsCurrentSeason),
            passes.Any(pass => pass.WasPurchased),
            administeredLeagues.Count,
            leaguesJoinedApproved,
            memberships.Count(membership => membership.Status == LeagueMemberStatus.Pending),
            prizes.Sum(prize => prize.Amount),
            SeasonPassSpend(passes),
            LeagueEntrySpend(memberships),
            Onboarding(user, data, passes.Count, memberships.Count, hasPayoutDetails),
            memberships,
            administeredLeagues,
            passes,
            prizes,
            Badges(data.Badges.Where(badge => badge.UserId == user.Id), seasons));
    }

    /// <summary>
    /// The leagues this account has asked to join, newest season first.
    /// </summary>
    /// <remarks>
    /// A row with no membership status is a league the account runs without playing in, which the schema allows - so it is
    /// not a membership and is filtered out here rather than being shown as one with a blank status.
    /// </remarks>
    private static List<UserLeagueMembershipDto> Memberships(
        IEnumerable<UserLeagueRow> leagues,
        IReadOnlyDictionary<int, SeasonFacts> seasons) =>
        leagues
            .Where(league => league.Status is not null)
            .OrderByDescending(league => league.SeasonId)
            .ThenBy(league => league.LeagueName, StringComparer.InvariantCultureIgnoreCase)
            .Select(league => new UserLeagueMembershipDto(
                league.LeagueId,
                league.LeagueName,
                league.SeasonId,
                SeasonNameOf(league.SeasonId, seasons),
                IsCurrentSeason(league.SeasonId, seasons),
                league.Status!.Value,
                league.IsFree,
                league.Price))
            .ToList();

    /// <summary>
    /// The leagues this account runs, newest season first.
    /// </summary>
    /// <remarks>
    /// <c>AlsoPlaying</c> is worked out from the same set of rows: a league the account both runs and belongs to arrives as
    /// two rows, and whether both exist is the question.
    /// </remarks>
    private static List<UserAdministeredLeagueDto> AdministeredLeagues(
        List<UserLeagueRow> leagues,
        IReadOnlyDictionary<int, SeasonFacts> seasons)
    {
        var playingIn = leagues
            .Where(league => league.Status == LeagueMemberStatus.Approved)
            .Select(league => league.LeagueId)
            .ToHashSet();

        return leagues
            .Where(league => league.IsAdministrator)
            .OrderByDescending(league => league.SeasonId)
            .ThenBy(league => league.LeagueName, StringComparer.InvariantCultureIgnoreCase)
            .Select(league => new UserAdministeredLeagueDto(
                league.LeagueId,
                league.LeagueName,
                league.SeasonId,
                SeasonNameOf(league.SeasonId, seasons),
                IsCurrentSeason(league.SeasonId, seasons),
                league.IsFree,
                league.Price,
                league.ApprovedMemberCount,
                playingIn.Contains(league.LeagueId)))
            .ToList();
    }

    private static List<UserSeasonPassDto> Passes(
        IEnumerable<UserSeasonPassRow> passes,
        IReadOnlyDictionary<int, SeasonFacts> seasons) =>
        passes
            .OrderByDescending(pass => pass.SeasonId)
            .Select(pass => new UserSeasonPassDto(
                pass.SeasonId,
                SeasonNameOf(pass.SeasonId, seasons),
                IsCurrentSeason(pass.SeasonId, seasons),
                pass.Tier,
                pass.Source,
                pass.AmountPaid,
                pass.SmsFeePaid,
                pass.CreatedAtUtc))
            .ToList();

    /// <summary>Prizes newest season first, and biggest first within a season.</summary>
    private static List<UserPrizeDto> Prizes(
        IEnumerable<UserWinningRow> winnings,
        IReadOnlyDictionary<int, SeasonFacts> seasons) =>
        winnings
            .OrderByDescending(winning => winning.SeasonId)
            .ThenByDescending(winning => winning.Amount)
            .Select(winning => new UserPrizeDto(
                winning.LeagueId,
                winning.LeagueName,
                winning.SeasonId,
                SeasonNameOf(winning.SeasonId, seasons),
                IsCurrentSeason(winning.SeasonId, seasons),
                UserPrizeTitle.Of(winning.PrizeType, winning.Stage, winning.RoundNumber, winning.Month),
                winning.Amount,
                winning.AwardedDateUtc))
            .ToList();

    /// <summary>
    /// One entry per badge earned, at the moment it was first earned, most recent first.
    /// </summary>
    /// <remarks>
    /// A repeatable badge has a row per round or per season it was won in, so a single account holds far more rows than
    /// the catalogue defines badges - somebody who has won fourteen rounds has fourteen Beat the Crowd rows. Counting
    /// those rows made the list report more badges than exist, so the rows are collapsed to the badge and only the first
    /// award of each survives. The later ones are the same badge won again, which is a story for the player's own badges
    /// page, not for an administrator telling accounts apart.
    ///
    /// The detail and the season come from that first award for the same reason: they describe the award being shown, so
    /// taking them from a later one would date-stamp one occasion and describe another.
    ///
    /// A key the catalogue no longer defines falls back to the key itself. Badges are defined in code and earned rows
    /// outlive the definition, so a badge retired from the catalogue still has rows pointing at it - showing the raw key is
    /// ugly but it is the truth, and it is better than an empty name or a crash.
    /// </remarks>
    private static List<UserBadgeDto> Badges(
        IEnumerable<UserBadgeRow> badges,
        IReadOnlyDictionary<int, SeasonFacts> seasons) =>
        badges
            .GroupBy(badge => badge.BadgeKey, StringComparer.Ordinal)
            .Select(group => group.OrderBy(badge => badge.AwardedUtc).First())
            .OrderByDescending(badge => badge.AwardedUtc)
            .Select(badge => new UserBadgeDto(
                badge.BadgeKey,
                BadgeCatalogue.Resolve(badge.BadgeKey)?.Name ?? badge.BadgeKey,
                badge.Detail,
                badge.AwardedUtc,
                badge.SeasonId,
                badge.SeasonId is null ? null : SeasonNameOf(badge.SeasonId.Value, seasons)))
            .ToList();

    /// <summary>
    /// How far this account got through the dashboard checklist.
    /// </summary>
    /// <remarks>
    /// The same registry the account sees on its own dashboard, fed the same four facts. Building a second, admin-only
    /// notion of "set up" would be a definition that could disagree with the one the player is looking at.
    ///
    /// The league count is every membership, not the approved ones - which is what the checklist has always counted, and
    /// is right: somebody whose request is still pending has joined a league as far as being onboarded goes.
    /// </remarks>
    private static Contracts.Onboarding.OnboardingChecklistDto Onboarding(
        AdminUserRow user,
        AdminUsersData data,
        int passCount,
        int membershipCount,
        bool hasPayoutDetails) =>
        OnboardingStepRegistry.Build(
            new OnboardingUserState(
                passCount,
                membershipCount,
                !string.IsNullOrWhiteSpace(user.PhoneNumber),
                hasPayoutDetails),
            data.OnboardingSkips
                .Where(skip => skip.UserId == user.Id)
                .Select(skip => skip.StepKey)
                .ToHashSet(StringComparer.Ordinal));

    /// <summary>
    /// What this account has paid for season passes.
    /// </summary>
    /// <remarks>
    /// Purchased passes only. A trial or a pass handed out by an administrator is still a pass - it is why the account can
    /// play - but it is not money anybody spent, and counting it would overstate what the site has taken.
    /// </remarks>
    private static decimal SeasonPassSpend(IEnumerable<UserSeasonPassDto> passes) =>
        passes
            .Where(pass => pass.WasPurchased)
            .Sum(pass => pass.TotalPaid);

    /// <summary>
    /// What this account has paid to enter leagues.
    /// </summary>
    /// <remarks>
    /// Three conditions, all of which were inside a subquery's <c>WHERE</c> clause. The membership has to have been
    /// approved, because a request that was never accepted was never paid for. The league has to be a paid one, and its
    /// price has to be above zero - two ways of saying the same thing that the data does not guarantee agree, so both are
    /// kept.
    /// </remarks>
    private static decimal LeagueEntrySpend(IEnumerable<UserLeagueMembershipDto> memberships) =>
        memberships
            .Where(membership => membership.Status == LeagueMemberStatus.Approved && !membership.IsFree && membership.Price > 0)
            .Sum(membership => membership.Price);

    private static string SeasonNameOf(int seasonId, IReadOnlyDictionary<int, SeasonFacts> seasons) =>
        seasons.TryGetValue(seasonId, out var season) ? season.Name : string.Empty;

    private static bool IsCurrentSeason(int seasonId, IReadOnlyDictionary<int, SeasonFacts> seasons) =>
        seasons.TryGetValue(seasonId, out var season) && season.IsCurrent;

    /// <summary>
    /// A season, with the finished-or-not question already settled.
    /// </summary>
    /// <remarks>
    /// Worked out once per season rather than once per pass, league and prize that points at it. An account with three
    /// passes, six memberships and eight prizes would otherwise ask the same question of the same season seventeen times.
    ///
    /// A season nothing knows about - which a foreign key makes impossible, but which a row type cannot promise - reads as
    /// unnamed and not current. Not current is the safe way round: it under-reports a pass rather than telling an
    /// administrator somebody is covered for a season when nothing can confirm it.
    /// </remarks>
    private sealed record SeasonFacts(string Name, bool IsCurrent)
    {
        public static SeasonFacts From(UserSeasonRow season) =>
            new(season.Name,
                !SeasonCompletion.IsEveryRoundComplete(
                    roundCount: season.RoundCount,
                    completedRoundCount: season.CompletedRoundCount));
    }
}
