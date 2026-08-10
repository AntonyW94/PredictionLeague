using FluentAssertions;
using ThePredictions.Application.Features.Boosts.Queries;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Boosts.Queries;

/// <summary>
/// The league's boost-usage table: who has used which boost, how many they have left, and whether the
/// window they were allowed to use it in has closed.
///
/// Hiding another player's boost for a round that is still open is <see cref="BoostUsageVisibility"/>'s
/// job, applied by the handler before calling this builder, so the usages reaching here have already been
/// censored. That rule now has its own unit tests rather than needing a database.
///
/// Two rules moved <i>into</i> this builder when the persistence split took the SQL out of the handler:
/// what a boost won, and how a player's name is displayed. Both are covered below.
/// </summary>
public class BoostUsageSummaryBuilderTests
{
    private const string CurrentUserId = "user-me";

    private static BoostRuleRow Rule(string code = "DOUBLE_UP", int totalUsesPerSeason = 2, int ruleId = 1) =>
        new(ruleId, code, "Double Up", "https://example.test/b.png", totalUsesPerSeason);

    private static BoostWindowRow Window(int start, int end, int maxUses = 1, int ruleId = 1) =>
        new(ruleId, start, end, maxUses);

    // The builder formats the display name, so members carry name parts. "Me" arrives as first name only.
    private static BoostMemberRow Member(string userId, string name) =>
        new(userId, name, string.Empty);

    // pointsGained is expressed as base/boosted, because computing the difference is now the builder's rule.
    // Null means the round has no result row yet.
    private static BoostUsageRow Usage(string userId, int roundNumber, int? pointsGained = 10, string code = "DOUBLE_UP") =>
        new(userId, code, roundNumber, RoundDeadlineUtc: default, HasBoost: pointsGained != null,
            BasePoints: pointsGained == null ? null : 0, BoostedPoints: pointsGained);

    private static BoostRoundRangeRow Range(int min = 1, int max = 38) =>
        new(min, max);

    /// <summary>
    /// The round range is passed as a nullable-of-nullable so a test can say "no range at all" and be
    /// distinguished from "not specified"; a plain default would silently substitute the usual range
    /// and the two no-range tests below would pass for the wrong reason.
    /// </summary>
    private static List<Contracts.Boosts.BoostUsageSummaryDto> Build(
        IReadOnlyList<BoostRuleRow>? rules = null,
        IReadOnlyList<BoostWindowRow>? windows = null,
        IReadOnlyList<BoostMemberRow>? members = null,
        IReadOnlyList<BoostUsageRow>? usages = null,
        Optional<BoostRoundRangeRow?> roundRange = default,
        int? inProgressRoundNumber = null,
        int? lastCompletedRoundNumber = null) =>
        BoostUsageSummaryBuilder.Build(
            rules ?? [Rule()],
            windows ?? [],
            members ?? [Member(CurrentUserId, "Me")],
            usages ?? [],
            roundRange.HasValue ? roundRange.Value : Range(),
            inProgressRoundNumber,
            lastCompletedRoundNumber,
            CurrentUserId);

    private readonly struct Optional<T>(T value)
    {
        public bool HasValue { get; } = true;
        public T Value { get; } = value;

        public static implicit operator Optional<T>(T value) => new(value);
    }

    // ---------- has the window closed? ----------

    // While a round is in progress that round is still live, so a window ending on it is still open.
    [Theory]
    [InlineData(4, 5, false, true)]   // window ended before the live round -> closed
    [InlineData(5, 5, false, false)]  // window ends on the live round -> still open
    [InlineData(6, 5, false, false)]  // window ends after the live round -> still open
    public void HasWindowPassed_ShouldCompareStrictly_WhenARoundIsInProgress(
        int windowEnd, int inProgressRound, bool _, bool expected)
    {
        BoostUsageSummaryBuilder.HasWindowPassed(windowEnd, inProgressRound, lastCompletedRoundNumber: 99)
            .Should().Be(expected);
    }

    // With nothing in progress the last completed round is finished, so a window ending on it has closed.
    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    public void HasWindowPassed_ShouldIncludeTheLastCompletedRound_WhenNothingIsInProgress(
        int windowEnd, int lastCompletedRound, bool expected)
    {
        BoostUsageSummaryBuilder.HasWindowPassed(windowEnd, inProgressRoundNumber: null, lastCompletedRound)
            .Should().Be(expected);
    }

    // A season that has not started has no closed windows at all.
    [Fact]
    public void HasWindowPassed_ShouldBeFalse_WhenTheSeasonHasNeitherMarker()
    {
        BoostUsageSummaryBuilder.HasWindowPassed(1, null, null).Should().BeFalse();
    }

    // The in-progress round wins: a season can have both markers, and the live round is the later one.
    [Fact]
    public void HasWindowPassed_ShouldPreferTheInProgressRound_WhenBothMarkersExist()
    {
        BoostUsageSummaryBuilder.HasWindowPassed(5, inProgressRoundNumber: 5, lastCompletedRoundNumber: 4)
            .Should().BeFalse();
    }

    // ---------- a boost with no configured windows ----------

    [Fact]
    public void Build_ShouldPresentAnUnwindowedBoostAsOneFullSeasonWindow()
    {
        var result = Build(rules: [Rule(totalUsesPerSeason: 3)], roundRange: Range(1, 38));

        var window = result.Single().Windows.Single();
        window.IsFullSeason.Should().BeTrue();
        window.StartRoundNumber.Should().Be(1);
        window.EndRoundNumber.Should().Be(38);
        window.MaxUsesInWindow.Should().Be(3);
    }

    // A league whose season has no rounds yet still has to render something rather than divide by nothing.
    [Fact]
    public void Build_ShouldFallBackToASingleRound_WhenTheSeasonHasNoRoundRange()
    {
        var result = Build(roundRange: (BoostRoundRangeRow?)null);

        var window = result.Single().Windows.Single();
        window.StartRoundNumber.Should().Be(1);
        window.EndRoundNumber.Should().Be(1);
    }

    [Fact]
    public void Build_ShouldCountEveryUsageAgainstTheSeasonAllowance_WhenTheBoostHasNoWindows()
    {
        var result = Build(
            rules: [Rule(totalUsesPerSeason: 3)],
            usages: [Usage(CurrentUserId, 2), Usage(CurrentUserId, 30)]);

        var player = result.Single().Windows.Single().PlayerUsages.Single();
        player.Usages.Should().HaveCount(2);
        player.Remaining.Should().Be(1);
    }

    // ---------- configured windows ----------

    [Fact]
    public void Build_ShouldProduceOneEntryPerWindow_InRoundOrder()
    {
        var result = Build(windows: [Window(20, 29), Window(1, 9), Window(10, 19)]);

        result.Single().Windows.Select(w => w.StartRoundNumber).Should().Equal(1, 10, 20);
    }

    [Fact]
    public void Build_ShouldOnlyCountUsagesInsideTheWindow()
    {
        var result = Build(
            windows: [Window(10, 19, maxUses: 2)],
            usages: [Usage(CurrentUserId, 9), Usage(CurrentUserId, 12), Usage(CurrentUserId, 20)]);

        var player = result.Single().Windows.Single().PlayerUsages.Single();
        player.Usages.Should().ContainSingle().Which.RoundNumber.Should().Be(12);
        player.Remaining.Should().Be(1);
    }

    // A single window spanning the season is shown as "full season" rather than as a round range that
    // tells the player nothing.
    [Fact]
    public void Build_ShouldTreatASingleSeasonSpanningWindowAsFullSeason()
    {
        var result = Build(windows: [Window(1, 38)], roundRange: Range(1, 38));

        result.Single().Windows.Single().IsFullSeason.Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldNotTreatAPartialWindowAsFullSeason()
    {
        var result = Build(windows: [Window(1, 19)], roundRange: Range(1, 38));

        result.Single().Windows.Single().IsFullSeason.Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldNotTreatMultipleWindowsAsFullSeason_EvenWhenTheyCoverTheSeason()
    {
        var result = Build(windows: [Window(1, 19), Window(20, 38)], roundRange: Range(1, 38));

        result.Single().Windows.Should().OnlyContain(w => !w.IsFullSeason);
    }

    [Fact]
    public void Build_ShouldNotTreatAWindowAsFullSeason_WhenTheRoundRangeIsUnknown()
    {
        var result = Build(windows: [Window(1, 38)], roundRange: (BoostRoundRangeRow?)null);

        result.Single().Windows.Single().IsFullSeason.Should().BeFalse();
    }

    // ---------- per-player figures ----------

    [Fact]
    public void Build_ShouldListEveryMember_IncludingThoseWhoHaveUsedNothing()
    {
        var result = Build(
            members: [Member(CurrentUserId, "Me"), Member("user-2", "Grace H")],
            usages: [Usage(CurrentUserId, 3)]);

        var players = result.Single().Windows.Single().PlayerUsages;
        players.Should().HaveCount(2);
        players.Single(p => p.UserId == "user-2").Usages.Should().BeEmpty();
    }

    [Fact]
    public void Build_ShouldFlagTheCurrentUser()
    {
        var result = Build(members: [Member(CurrentUserId, "Me"), Member("user-2", "Grace H")]);

        var players = result.Single().Windows.Single().PlayerUsages;
        players.Single(p => p.UserId == CurrentUserId).IsCurrentUser.Should().BeTrue();
        players.Single(p => p.UserId == "user-2").IsCurrentUser.Should().BeFalse();
    }

    // A window whose allowance was reduced after the fact can leave a player already over it, and the
    // page has no way to show "-1 remaining".
    [Fact]
    public void Build_ShouldNeverReportNegativeRemaining_WhenAPlayerIsOverTheAllowance()
    {
        var result = Build(
            windows: [Window(1, 38, maxUses: 1)],
            usages: [Usage(CurrentUserId, 2), Usage(CurrentUserId, 3), Usage(CurrentUserId, 4)]);

        result.Single().Windows.Single().PlayerUsages.Single().Remaining.Should().Be(0);
    }

    [Fact]
    public void Build_ShouldListAPlayersUsagesInRoundOrder()
    {
        var result = Build(usages: [Usage(CurrentUserId, 30), Usage(CurrentUserId, 2), Usage(CurrentUserId, 11)]);

        result.Single().Windows.Single().PlayerUsages.Single()
            .Usages.Select(u => u.RoundNumber).Should().Equal(2, 11, 30);
    }

    [Fact]
    public void Build_ShouldFlagAUsageInTheRoundBeingPlayed()
    {
        var result = Build(
            usages: [Usage(CurrentUserId, 5), Usage(CurrentUserId, 6)],
            inProgressRoundNumber: 6);

        var usages = result.Single().Windows.Single().PlayerUsages.Single().Usages;
        usages.Single(u => u.RoundNumber == 5).IsInProgressRound.Should().BeFalse();
        usages.Single(u => u.RoundNumber == 6).IsInProgressRound.Should().BeTrue();
    }

    // ---------- ordering ----------

    [Fact]
    public void Build_ShouldOrderPlayersByPointsTheirBoostsWon()
    {
        var result = Build(
            members: [Member("user-1", "Ada L"), Member("user-2", "Grace H"), Member("user-3", "Zoe W")],
            usages: [Usage("user-1", 2, pointsGained: 5), Usage("user-2", 2, pointsGained: 22)]);

        result.Single().Windows.Single().PlayerUsages
            .Select(p => p.PlayerName).Should().Equal("Grace H", "Ada L", "Zoe W");
    }

    // A boost played in a round still being scored has no points yet. Counting that as zero keeps the
    // player where they were rather than pushing them down for having used one.
    [Fact]
    public void Build_ShouldTreatAnUnscoredBoostAsZeroPoints_WhenOrdering()
    {
        var result = Build(
            members: [Member("user-1", "Ada L"), Member("user-2", "Grace H")],
            usages: [Usage("user-1", 6, pointsGained: null), Usage("user-2", 2, pointsGained: 3)],
            inProgressRoundNumber: 6);

        result.Single().Windows.Single().PlayerUsages
            .Select(p => p.PlayerName).Should().Equal("Grace H", "Ada L");
    }

    [Fact]
    public void Build_ShouldFallBackToPlayerName_WhenPointsAreEqual()
    {
        var result = Build(members: [Member("user-3", "Zoe W"), Member("user-1", "Ada L"), Member("user-2", "Grace H")]);

        result.Single().Windows.Single().PlayerUsages
            .Select(p => p.PlayerName).Should().Equal("Ada L", "Grace H", "Zoe W");
    }

    // ---------- several boosts ----------

    [Fact]
    public void Build_ShouldKeepEachBoostsUsagesToItself()
    {
        var result = Build(
            rules: [Rule("DOUBLE_UP", ruleId: 1), Rule("WILDCARD", ruleId: 2)],
            windows: [Window(1, 38, ruleId: 1), Window(1, 38, ruleId: 2)],
            usages: [Usage(CurrentUserId, 3, code: "DOUBLE_UP")]);

        result.Should().HaveCount(2);
        result.Single(r => r.BoostCode == "DOUBLE_UP").Windows.Single().PlayerUsages.Single().Usages.Should().ContainSingle();
        result.Single(r => r.BoostCode == "WILDCARD").Windows.Single().PlayerUsages.Single().Usages.Should().BeEmpty();
    }

    [Fact]
    public void Build_ShouldCarryTheBoostDetailsThrough()
    {
        var result = Build(rules: [Rule(totalUsesPerSeason: 4)]);

        var boost = result.Single();
        boost.BoostCode.Should().Be("DOUBLE_UP");
        boost.Name.Should().Be("Double Up");
        boost.ImageUrl.Should().Be("https://example.test/b.png");
        boost.TotalUsesPerSeason.Should().Be(4);
    }

    [Fact]
    public void Build_ShouldReturnNothing_WhenTheLeagueHasNoBoostRules()
    {
        Build(rules: []).Should().BeEmpty();
    }
}
