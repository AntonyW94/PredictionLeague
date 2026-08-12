using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using ThePredictions.Domain.Services.Prizes;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's winnings page: every prize on offer, who has won each one, and what each member has taken in total.
/// </summary>
/// <remarks>
/// Almost all of this was already C#. What moved is the four reads behind it - and with them the two places that
/// formatted a month name using the machine's locale and then <b>parsed it back</b> to sort by it.
/// </remarks>
public class GetWinningsQueryHandler(
    IWinningsQuery winningsQuery,
    ILeagueMembershipService membershipService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetWinningsQuery, WinningsDto>
{
    public async Task<WinningsDto> Handle(GetWinningsQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await winningsQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (data == null)
            return new WinningsDto();

        var header = data.Header;

        if (!AreWinningsWorkedOut(header, data.PrizeSettings, dateTimeProvider.UtcNow))
        {
            return new WinningsDto
            {
                WinningsCalculated = false,
                EntryCount = header.EntryCount,
                EntryCost = header.EntryCost,
                TotalPrizePot = TotalPrizePot(header)
            };
        }

        return new WinningsDto
        {
            WinningsCalculated = true,
            EntryCount = header.EntryCount,
            EntryCost = header.EntryCost,
            TotalPrizePot = TotalPrizePot(header),
            RoundPrizes = RoundPrizes(data),
            MonthlyPrizes = MonthlyPrizes(data),
            StagePrizes = StagePrizes(data),
            EndOfSeasonPrizes = EndOfSeasonPrizes(data),
            Leaderboard = new WinningsLeaderboardDto { Entries = Leaderboard(data) }
        };
    }

    /// <summary>
    /// Whether there is anything to show yet: entries must have closed, and the league must actually be offering prizes.
    /// </summary>
    /// <remarks>
    /// A league whose entry deadline has not passed shows its pot and nothing else, because who is competing is still
    /// changing. A league with no prizes configured shows the same, because there is nothing to win.
    ///
    /// A league with no deadline at all counts as closed. The old comparison was <c>EntryDeadlineUtc &gt; now</c> against a
    /// non-nullable field, so a null would have failed to materialise rather than reaching this decision.
    /// </remarks>
    private static bool AreWinningsWorkedOut(
        WinningsHeaderRow header,
        IReadOnlyList<WinningsPrizeSettingRow> prizeSettings,
        DateTime utcNow)
    {
        if (header.EntryDeadlineUtc is { } deadline && deadline > utcNow)
            return false;

        return prizeSettings.Count > 0;
    }

    /// <summary>
    /// The pot: every entry fee, plus whatever the administrator has put in on top.
    /// </summary>
    /// <remarks>
    /// <c>PrizeFund.Total</c>, the same as the league dashboard, the My Leagues tile and the available-leagues list. This
    /// page worked it out as <c>EntryCount * EntryCost</c> and left the top-up out, so a league with a funded top-up showed
    /// a smaller pot here than on its own dashboard.
    /// </remarks>
    private static decimal TotalPrizePot(WinningsHeaderRow header) =>
        PrizeFund.Total(header.EntryCost, header.EntryCount, header.PrizeFundOverride);

    /// <summary>
    /// One line per round of the season: the winner where there is one, and the prize still on offer where there is not.
    /// </summary>
    /// <remarks>
    /// The round numbers are carried through and only turned into text at the end. The old code named each line with
    /// <c>winner.RoundNumber.ToString()</c> and then sorted the list with <c>int.Parse(p.Name)</c> - so a round prize
    /// recorded without a round number produced an empty name and took the whole page down on the sort.
    ///
    /// Such a win is skipped now rather than crashing the page. It should not exist - a round prize is won in a round - but
    /// a prize missing from a list is a far better failure than a page that will not load.
    /// </remarks>
    private static List<PrizeDto> RoundPrizes(WinningsData data)
    {
        var setting = data.PrizeSettings.FirstOrDefault(prize => prize.PrizeType == PrizeType.Round);

        if (setting == null)
            return [];

        var won = data.Winnings
            .Where(winning => winning.PrizeType == PrizeType.Round && winning.RoundNumber.HasValue)
            .ToList();

        var wonRoundNumbers = won.Select(winning => winning.RoundNumber!.Value).ToHashSet();

        var claimed = won.Select(winning => new NumberedPrize(
            winning.RoundNumber!.Value,
            winning.Amount,
            NameOf(winning),
            winning.UserId));

        var unclaimed = Enumerable.Range(1, data.Header.TotalRoundsInSeason)
            .Where(roundNumber => !wonRoundNumbers.Contains(roundNumber))
            .Select(roundNumber => new NumberedPrize(roundNumber, setting.Amount, null, null));

        return claimed.Concat(unclaimed)
            .OrderBy(prize => prize.Number)
            .Select(prize => new PrizeDto
            {
                Name = prize.Number.ToString(),
                Amount = prize.Amount,
                Winner = prize.Winner,
                UserId = prize.UserId
            })
            .ToList();
    }

    /// <summary>
    /// One line per month of the season, in the order the season runs them.
    /// </summary>
    /// <remarks>
    /// The month numbers are carried all the way to the last step and only turned into names there. The old code formatted
    /// the name first and then sorted by <c>DateTime.ParseExact(name, "MMMM", CurrentCulture)</c> - a round trip through a
    /// localised string, which fails outright if the culture formatting it is not the culture parsing it.
    /// </remarks>
    private static List<PrizeDto> MonthlyPrizes(WinningsData data)
    {
        var setting = data.PrizeSettings.FirstOrDefault(prize => prize.PrizeType == PrizeType.Monthly);

        if (setting == null)
            return [];

        var won = data.Winnings
            .Where(winning => winning.PrizeType == PrizeType.Monthly && winning.Month.HasValue)
            .ToList();

        var wonMonths = won.Select(winning => winning.Month!.Value).ToHashSet();

        var claimed = won.Select(winning => new NumberedPrize(
            winning.Month!.Value,
            winning.Amount,
            NameOf(winning),
            winning.UserId));

        var unclaimed = SeasonMonths(data.Header)
            .Where(month => !wonMonths.Contains(month))
            .Select(month => new NumberedPrize(month, setting.Amount, null, null));

        var all = claimed.Concat(unclaimed).ToList();

        return SeasonMonthOrder.Apply(all, prize => prize.Number, data.Header.SeasonStartDateUtc.Month)
            .Select(prize => new PrizeDto
            {
                Name = MonthName.Of(prize.Number)!,
                Amount = prize.Amount,
                Winner = prize.Winner,
                UserId = prize.UserId
            })
            .ToList();
    }

    /// <summary>
    /// Every month the season touches, once each.
    /// </summary>
    /// <remarks>
    /// Distinct because a season spanning more than a year would otherwise offer the same month twice. Not reachable with
    /// a real season, and the old code would have listed the duplicate.
    /// </remarks>
    private static IEnumerable<int> SeasonMonths(WinningsHeaderRow header)
    {
        var months = new List<int>();

        for (var month = header.SeasonStartDateUtc; month <= header.SeasonEndDateUtc; month = month.AddMonths(1))
            months.Add(month.Month);

        return months.Distinct();
    }

    /// <summary>
    /// The tournament stage prizes, by stage and then biggest first.
    /// </summary>
    private static List<PrizeDto> StagePrizes(WinningsData data) =>
        PrizesFor(
            data,
            data.PrizeSettings
                .Where(prize => prize.PrizeType == PrizeType.Stages)
                .OrderBy(prize => prize.Stage, StringComparer.InvariantCultureIgnoreCase)
                .ThenByDescending(prize => prize.Amount));

    /// <summary>
    /// The prizes settled at the end of the season, by kind and then biggest first.
    /// </summary>
    private static List<PrizeDto> EndOfSeasonPrizes(WinningsData data) =>
        PrizesFor(
            data,
            data.PrizeSettings
                .Where(prize => PrizeCategoryRegistry.IsEndOfSeason(prize.PrizeType))
                .OrderBy(prize => prize.PrizeType)
                .ThenByDescending(prize => prize.Amount));

    /// <summary>
    /// Turns prize settings into lines: one per winner, or one showing what is still on offer.
    /// </summary>
    /// <remarks>
    /// One prize can have several winners - a shared prize is split between them - which is why a claimed prize expands to
    /// a line each rather than one line with a list of names.
    /// </remarks>
    private static List<PrizeDto> PrizesFor(WinningsData data, IEnumerable<WinningsPrizeSettingRow> settings)
    {
        var prizes = new List<PrizeDto>();

        foreach (var setting in settings)
        {
            var winners = data.Winnings
                .Where(winning => winning.LeaguePrizeSettingId == setting.Id)
                .ToList();

            if (winners.Count == 0)
            {
                prizes.Add(new PrizeDto
                {
                    Name = setting.Name!,
                    Amount = setting.Amount,
                    Winner = null,
                    UserId = null
                });

                continue;
            }

            prizes.AddRange(winners.Select(winner => new PrizeDto
            {
                Name = setting.Name!,
                Amount = winner.Amount,
                Winner = NameOf(winner),
                UserId = winner.UserId
            }));
        }

        return prizes;
    }

    /// <summary>
    /// What each member has won, split by the kind of prize, biggest total first.
    /// </summary>
    /// <remarks>
    /// Every approved member appears, including those who have won nothing - the table is the league, not a list of
    /// winners.
    /// </remarks>
    private static List<WinningsLeaderboardEntryDto> Leaderboard(WinningsData data)
    {
        var winningsByUser = data.Winnings
            .GroupBy(winning => winning.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return data.Members
            .Select(member =>
            {
                var won = winningsByUser.GetValueOrDefault(member.UserId) ?? [];

                return new WinningsLeaderboardEntryDto
                {
                    PlayerName = PlayerDisplayName.Format(member.FirstName, member.LastName),
                    RoundWinnings = TotalOf(won, PrizeType.Round),
                    MonthlyWinnings = TotalOf(won, PrizeType.Monthly),
                    StageWinnings = TotalOf(won, PrizeType.Stages),
                    EndOfSeasonWinnings = won
                        .Where(winning => PrizeCategoryRegistry.IsEndOfSeason(winning.PrizeType))
                        .Sum(winning => winning.Amount),
                    TotalWinnings = won.Sum(winning => winning.Amount),
                    UserId = member.UserId
                };
            })
            .OrderByDescending(entry => entry.TotalWinnings)
            .ThenBy(entry => entry.PlayerName, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    private static decimal TotalOf(IEnumerable<WinningsRow> winnings, PrizeType prizeType) =>
        winnings.Where(winning => winning.PrizeType == prizeType).Sum(winning => winning.Amount);

    private static string NameOf(WinningsRow winning) =>
        PlayerDisplayName.Format(winning.FirstName, winning.LastName);

    /// <summary>
    /// One prize for a numbered period - a round or a month - before that number becomes a name. Keeping the number until
    /// the last step is what removes the old code's round trip through a formatted string.
    /// </summary>
    private sealed record NumberedPrize(int Number, decimal Amount, string? Winner, string? UserId);
}
