using MediatR;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>
/// The welcome emails waiting to go out: for each league that has just closed to entry, everybody in it who has not been welcomed
/// yet, and what the league offers them.
/// </summary>
public class GetLeagueWelcomeBatchQueryHandler(ILeagueWelcomeBatchQuery welcomeBatchQuery)
    : IRequestHandler<GetLeagueWelcomeBatchQuery, IReadOnlyList<LeagueWelcomeLeague>>
{
    public async Task<IReadOnlyList<LeagueWelcomeLeague>> Handle(
        GetLeagueWelcomeBatchQuery request,
        CancellationToken cancellationToken)
    {
        var data = await welcomeBatchQuery.ExecuteAsync(request.WindowStartUtc, request.NowUtc, cancellationToken);

        return data.Leagues
            .Where(league => IsReadyToWelcome(league, data))
            .OrderBy(league => league.LeagueId)
            .Select(league => ToWelcomeLeague(league, RecipientsFor(league, data), data))
            .Where(league => league.Recipients.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Whether this league is in a fit state to be welcomed.
    /// </summary>
    /// <remarks>
    /// A league whose administrator has set up a prize scheme but whose individual prizes have not been worked out from it yet is
    /// half-configured. Welcoming its members would send them an email about prizes with nothing in the list, so the whole league
    /// waits until the next run. This was a <c>NOT EXISTS</c> nested inside another <c>NOT EXISTS</c>, four levels into the
    /// statement.
    /// </remarks>
    private static bool IsReadyToWelcome(WelcomeLeagueRow league, LeagueWelcomeBatchData data)
    {
        var hasScheme = data.Schemes.Any(scheme => scheme.LeagueId == league.LeagueId);

        if (!hasScheme)
            return true;

        return data.Prizes.Any(prize => prize.LeagueId == league.LeagueId);
    }

    /// <summary>
    /// Who in this league still needs welcoming, in a stable order.
    /// </summary>
    /// <remarks>
    /// Anybody already in the sent-log is left out. Sending the same welcome twice is the one failure this job has to avoid, and
    /// the check is per league and per player because somebody can be welcomed to one league and not another.
    /// </remarks>
    private static List<LeagueWelcomeRecipient> RecipientsFor(WelcomeLeagueRow league, LeagueWelcomeBatchData data) =>
        data.Recipients
            .Where(recipient => recipient.LeagueId == league.LeagueId && !HasAlreadyBeenWelcomed(recipient, data))
            .OrderBy(recipient => recipient.FirstName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(recipient => recipient.UserId, StringComparer.Ordinal)
            .Select(recipient => new LeagueWelcomeRecipient(recipient.UserId, recipient.Email, recipient.FirstName ?? string.Empty))
            .ToList();

    private static bool HasAlreadyBeenWelcomed(WelcomeRecipientRow recipient, LeagueWelcomeBatchData data) =>
        data.AlreadyNotified.Any(sent => sent.LeagueId == recipient.LeagueId && sent.UserId == recipient.UserId);

    private static LeagueWelcomeLeague ToWelcomeLeague(
        WelcomeLeagueRow league,
        List<LeagueWelcomeRecipient> recipients,
        LeagueWelcomeBatchData data) =>
        new(league.LeagueId,
            league.LeagueName,
            league.SeasonName,
            league.HasPrizes,
            league.MemberCount,
            league.NumberOfRounds,
            CountMonths(league.SeasonStartDateUtc, league.SeasonEndDateUtc),
            PrizesFor(league, data),
            BoostsFor(league, data),
            recipients);

    private static List<LeagueWelcomePrize> PrizesFor(WelcomeLeagueRow league, LeagueWelcomeBatchData data) =>
        data.Prizes
            .Where(prize => prize.LeagueId == league.LeagueId)
            .Select(prize => new LeagueWelcomePrize(prize.PrizeType, prize.Rank, prize.Stage, prize.Amount))
            .ToList();

    /// <summary>
    /// The boosts this league offers, with the stretches of rounds each can be used in.
    /// </summary>
    /// <remarks>
    /// Only the ones switched on: the email is telling somebody what they can do this season, and a disabled rule is not something
    /// they can do. That was <c>AND lbr.[IsEnabled] = 1</c> in two of the four statements, which also meant a window belonging to a
    /// disabled rule had to be filtered out separately.
    /// </remarks>
    private static List<LeagueWelcomeBoost> BoostsFor(WelcomeLeagueRow league, LeagueWelcomeBatchData data) =>
        data.Boosts
            .Where(boost => boost.LeagueId == league.LeagueId && boost.IsEnabled)
            .Select(boost => new LeagueWelcomeBoost(
                boost.Name,
                boost.Description,
                boost.ImageUrl,
                boost.TotalUsesPerSeason,
                WindowsFor(boost, data)))
            .ToList();

    private static List<LeagueWelcomeBoostWindow> WindowsFor(WelcomeBoostRow boost, LeagueWelcomeBatchData data) =>
        data.BoostWindows
            .Where(window => window.LeagueBoostRuleId == boost.RuleId)
            .OrderBy(window => window.StartRoundNumber)
            .Select(window => new LeagueWelcomeBoostWindow(window.StartRoundNumber, window.EndRoundNumber, window.MaxUsesInWindow))
            .ToList();

    /// <summary>
    /// How many calendar months the season touches, counted inclusively from its start.
    /// </summary>
    /// <remarks>
    /// Every month the season runs through gets a monthly prize, including a first or last month it only partly covers - which is
    /// why this steps month by month rather than subtracting the dates.
    /// </remarks>
    private static int CountMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        var months = 0;

        for (var date = startDateUtc; date <= endDateUtc; date = date.AddMonths(1))
            months++;

        return months;
    }
}
