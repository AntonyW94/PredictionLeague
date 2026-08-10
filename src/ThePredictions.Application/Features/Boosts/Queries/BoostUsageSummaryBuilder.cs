using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// Turns the six reads behind the league's boost-usage table into the shape the page renders. Split
/// out of <see cref="GetLeagueBoostUsageSummaryQueryHandler"/> because none of it needs a database:
/// it is a pure function of rows in and DTOs out, and it holds the rules that are easy to get subtly
/// wrong - whether a window has closed, how many uses a player has left, and the order players appear in.
///
/// It does <b>not</b> hold the secrecy rule: the usages arriving here have already been censored by
/// <see cref="BoostUsageVisibility"/>, which the handler applies against an injected clock. That rule used to
/// be a SQL predicate reading <c>GETUTCDATE()</c>, which no unit test could reach and no test could pin to an
/// instant.
///
/// It does now hold two rules that used to be SQL. What a boost <b>won</b> is boosted points less base
/// points, and only when a boost was actually applied - previously a <c>CASE</c> expression in the read. And
/// a player's display name is <see cref="PlayerDisplayName"/>, previously
/// <c>FirstName + ' ' + LEFT(LastName, 1)</c> written out in seventeen different files.
/// </summary>
internal static class BoostUsageSummaryBuilder
{
    internal static List<BoostUsageSummaryDto> Build(
        IReadOnlyList<BoostRuleRow> boostRules,
        IReadOnlyList<BoostWindowRow> windows,
        IReadOnlyList<BoostMemberRow> members,
        IReadOnlyList<BoostUsageRow> usages,
        BoostRoundRangeRow? roundRange,
        int? inProgressRoundNumber,
        int? lastCompletedRoundNumber,
        string currentUserId)
    {
        var result = new List<BoostUsageSummaryDto>();

        foreach (var rule in boostRules)
        {
            var ruleWindows = windows
                .Where(w => w.LeagueBoostRuleId == rule.LeagueBoostRuleId)
                .OrderBy(w => w.StartRoundNumber)
                .ToList();

            var boostUsages = usages.Where(u => u.BoostCode == rule.BoostCode).ToList();

            var windowDtos = ruleWindows.Count == 0
                ? [BuildSeasonWideWindow(rule, members, boostUsages, roundRange, inProgressRoundNumber, lastCompletedRoundNumber, currentUserId)]
                : BuildConfiguredWindows(ruleWindows, members, boostUsages, roundRange, inProgressRoundNumber, lastCompletedRoundNumber, currentUserId);

            result.Add(new BoostUsageSummaryDto
            {
                BoostCode = rule.BoostCode,
                Name = rule.Name,
                ImageUrl = rule.ImageUrl,
                TotalUsesPerSeason = rule.TotalUsesPerSeason,
                Windows = windowDtos
            });
        }

        return result;
    }

    /// <summary>
    /// A boost with no configured windows is usable all season, so it is presented as a single window
    /// spanning the season's own round range and capped by the per-season allowance.
    /// </summary>
    /// <summary>
    /// What a boost actually won: the difference between the boosted and base points for that round, and only
    /// where a boost was applied and the round has been scored. Null means "no points to report yet" rather
    /// than zero, so the page can distinguish a boost that gained nothing from one not yet counted.
    /// </summary>
    /// <remarks>
    /// Written as statements rather than a property pattern deliberately. The equivalent
    /// <c>usage is { HasBoost: true, BasePoints: not null, BoostedPoints: not null }</c> reads well but
    /// lowers to ten branches for three conditions, so the 100% branch gate cannot be satisfied without
    /// contriving tests for outcomes the logic does not actually have. This form's branches match its
    /// meaning.
    /// </remarks>
    internal static int? PointsGained(BoostUsageRow usage)
    {
        if (!usage.HasBoost)
            return null;

        if (usage.BasePoints == null || usage.BoostedPoints == null)
            return null;

        return usage.BoostedPoints.Value - usage.BasePoints.Value;
    }

    private static WindowUsageSummaryDto BuildSeasonWideWindow(
        BoostRuleRow rule,
        IReadOnlyList<BoostMemberRow> members,
        List<BoostUsageRow> boostUsages,
        BoostRoundRangeRow? roundRange,
        int? inProgressRoundNumber,
        int? lastCompletedRoundNumber,
        string currentUserId)
    {
        var endRound = roundRange?.MaxRoundNumber ?? 1;

        return new WindowUsageSummaryDto
        {
            StartRoundNumber = roundRange?.MinRoundNumber ?? 1,
            EndRoundNumber = endRound,
            MaxUsesInWindow = rule.TotalUsesPerSeason,
            IsFullSeason = true,
            HasWindowPassed = HasWindowPassed(endRound, inProgressRoundNumber, lastCompletedRoundNumber),
            // No window bounds: every usage of this boost counts against the season allowance.
            PlayerUsages = BuildPlayerUsages(members, boostUsages, null, null, rule.TotalUsesPerSeason, currentUserId, inProgressRoundNumber)
        };
    }

    private static List<WindowUsageSummaryDto> BuildConfiguredWindows(
        List<BoostWindowRow> ruleWindows,
        IReadOnlyList<BoostMemberRow> members,
        List<BoostUsageRow> boostUsages,
        BoostRoundRangeRow? roundRange,
        int? inProgressRoundNumber,
        int? lastCompletedRoundNumber,
        string currentUserId)
    {
        // A single window covering the whole season is presented as "full season" rather than as a
        // window, so the page does not show a round range that adds nothing.
        var isFullSeason = ruleWindows.Count == 1
            && roundRange != null
            && ruleWindows[0].StartRoundNumber <= roundRange.MinRoundNumber
            && ruleWindows[0].EndRoundNumber >= roundRange.MaxRoundNumber;

        return ruleWindows
            .Select(window => new WindowUsageSummaryDto
            {
                StartRoundNumber = window.StartRoundNumber,
                EndRoundNumber = window.EndRoundNumber,
                MaxUsesInWindow = window.MaxUsesInWindow,
                IsFullSeason = isFullSeason,
                HasWindowPassed = HasWindowPassed(window.EndRoundNumber, inProgressRoundNumber, lastCompletedRoundNumber),
                PlayerUsages = BuildPlayerUsages(members, boostUsages, window.StartRoundNumber, window.EndRoundNumber, window.MaxUsesInWindow, currentUserId, inProgressRoundNumber)
            })
            .ToList();
    }

    /// <summary>
    /// Whether a window has closed and so can no longer be used.
    /// </summary>
    /// <remarks>
    /// The comparison deliberately differs depending on which marker the season has. While a round is
    /// in progress that round is still live, so a window ending on it has <b>not</b> passed and the
    /// test is strict. With no round in progress the last completed round is finished, so a window
    /// ending on it <b>has</b> passed and the test includes it. Getting these the same way round would
    /// either close a window a player can still use, or leave a finished one looking open.
    /// </remarks>
    internal static bool HasWindowPassed(int windowEndRoundNumber, int? inProgressRoundNumber, int? lastCompletedRoundNumber)
    {
        if (inProgressRoundNumber.HasValue)
            return windowEndRoundNumber < inProgressRoundNumber.Value;

        return lastCompletedRoundNumber.HasValue
               && windowEndRoundNumber <= lastCompletedRoundNumber.Value;
    }

    /// <summary>
    /// One row per league member for a single window, ordered by the points their boosts actually won
    /// them - the table's headline is who used theirs best, not who used one first.
    /// </summary>
    internal static List<PlayerWindowUsageDto> BuildPlayerUsages(
        IReadOnlyList<BoostMemberRow> members,
        List<BoostUsageRow> boostUsages,
        int? startRound,
        int? endRound,
        int maxUses,
        string currentUserId,
        int? inProgressRoundNumber)
    {
        return members.Select(member =>
        {
            var memberUsages = boostUsages.Where(u => u.UserId == member.UserId);

            if (startRound.HasValue && endRound.HasValue)
                memberUsages = memberUsages.Where(u => u.RoundNumber >= startRound.Value && u.RoundNumber <= endRound.Value);

            var usageList = memberUsages.ToList();

            return new PlayerWindowUsageDto
            {
                UserId = member.UserId,
                PlayerName = PlayerDisplayName.Format(member.FirstName, member.LastName),
                // Clamped: a window whose allowance was later reduced can leave a player already over
                // it, and "-1 remaining" is not a thing the page can show.
                Remaining = Math.Max(0, maxUses - usageList.Count),
                MaxUses = maxUses,
                IsCurrentUser = member.UserId == currentUserId,
                Usages = usageList
                    .OrderBy(u => u.RoundNumber)
                    .Select(u => new BoostUsageDetailDto
                    {
                        RoundNumber = u.RoundNumber,
                        PointsGained = PointsGained(u),
                        IsInProgressRound = inProgressRoundNumber.HasValue && u.RoundNumber == inProgressRoundNumber.Value
                    })
                    .ToList()
            };
        })
        // A boost played in a round still being scored has no points yet, so it counts as zero here
        // rather than dropping the player down the table for having used one.
        .OrderByDescending(p => p.Usages.Sum(u => u.PointsGained ?? 0))
        .ThenBy(p => p.PlayerName)
        .ToList();
    }
}
