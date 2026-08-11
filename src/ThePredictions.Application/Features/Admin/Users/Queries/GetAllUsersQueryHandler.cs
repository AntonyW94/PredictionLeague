using MediatR;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// The administrator's list of every account, with what each one has joined, held and spent.
/// </summary>
public class GetAllUsersQueryHandler(IAdminUsersQuery adminUsersQuery)
    : IRequestHandler<GetAllUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var data = await adminUsersQuery.ExecuteAsync(cancellationToken);

        return data.Users
            .Select(user => ToDto(user, data))
            .OrderBy(user => user.FullName, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    private static UserDto ToDto(AdminUserRow user, AdminUsersData data)
    {
        var leagues = data.Leagues.Where(league => league.UserId == user.Id).ToList();
        var passes = data.SeasonPasses.Where(pass => pass.UserId == user.Id).ToList();

        return new UserDto(
            user.Id,
            // The full name, not the abbreviated one players see: this screen is for telling accounts apart.
            PlayerDisplayName.FormatFull(user.FirstName, user.LastName),
            user.Email,
            user.PhoneNumber,
            user.IsAdmin,
            user.HasPassword,
            data.LoginProviders
                .Where(provider => provider.UserId == user.Id)
                .Select(provider => provider.LoginProvider)
                .ToList(),
            user.EmailConfirmed,
            passes.Count > 0,
            leagues.Count(league => league.IsAdministrator),
            leagues.Count(league => league.Status == LeagueMemberStatus.Approved),
            leagues.Count(league => league.Status == LeagueMemberStatus.Pending),
            data.Winnings.Where(winning => winning.UserId == user.Id).Sum(winning => winning.Amount),
            SeasonPassSpend(passes),
            LeagueEntrySpend(leagues));
    }

    /// <summary>
    /// What this account has paid for season passes.
    /// </summary>
    /// <remarks>
    /// Purchased passes only. A trial or a pass handed out by an administrator is still a pass - it counts towards "has a
    /// season pass" - but it is not money anybody spent, and counting it would overstate what the site has taken.
    /// </remarks>
    private static decimal SeasonPassSpend(IEnumerable<UserSeasonPassRow> passes) =>
        passes
            .Where(pass => pass.Source == SeasonPassSource.Purchased)
            .Sum(pass => pass.AmountPaid + pass.SmsFeePaid);

    /// <summary>
    /// What this account has paid to enter leagues.
    /// </summary>
    /// <remarks>
    /// Three conditions, all of which were inside a subquery's <c>WHERE</c> clause. The membership has to have been
    /// approved, because a request that was never accepted was never paid for. The league has to be a paid one, and its
    /// price has to be above zero - two ways of saying the same thing that the data does not guarantee agree, so both are
    /// kept.
    /// </remarks>
    private static decimal LeagueEntrySpend(IEnumerable<UserLeagueRow> leagues) =>
        leagues
            .Where(league => league.Status == LeagueMemberStatus.Approved && !league.IsFree && league.Price > 0)
            .Sum(league => league.Price);
}
