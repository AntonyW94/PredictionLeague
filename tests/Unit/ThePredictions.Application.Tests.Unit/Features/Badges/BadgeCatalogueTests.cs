using FluentAssertions;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Contracts.Badges;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges;

public class BadgeCatalogueTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    private static BadgeProgressMetrics Metrics(
        int seasonExactTotal = 0,
        int bestExactsInRound = 0,
        int bestStreak = 0,
        int currentStreak = 0,
        int leaguesJoined = 0,
        EverPresentProgress? everPresent = null) =>
        new(seasonExactTotal, bestExactsInRound, bestStreak, currentStreak, leaguesJoined, everPresent);

    private static BadgeUserState State(
        IReadOnlyDictionary<string, EarnedBadge>? earned = null,
        BadgeProgressMetrics? metrics = null) =>
        new(earned ?? new Dictionary<string, EarnedBadge>(), metrics ?? Metrics());

    private static Dictionary<string, EarnedBadge> Earned(params (string Key, DateTime AwardedUtc)[] badges) =>
        badges.ToDictionary(b => b.Key, b => new EarnedBadge(b.Key, 1, b.AwardedUtc, null));

    private static BadgeDto Group(BadgeUserState state, string groupKey) =>
        BadgeCatalogue.BuildPage(state, NowUtc)
            .Collections.Concat(BadgeCatalogue.BuildPage(state, NowUtc).Badges)
            .Concat(BadgeCatalogue.BuildPage(state, NowUtc).Honours)
            .Single(d => d.Key == groupKey);

    // ---------- Resolve ----------

    [Fact]
    public void Resolve_ShouldReturnNull_WhenTheKeyIsUnknown()
    {
        BadgeCatalogue.Resolve("not-a-badge").Should().BeNull();
    }

    [Theory]
    [InlineData(BadgeKeys.Marksman1, 1, "bronze")]
    [InlineData(BadgeKeys.Marksman2, 2, "silver")]
    [InlineData(BadgeKeys.Marksman3, 3, "gold")]
    public void Resolve_ShouldStepTheVariantByTier_ForCollectionBadges(string key, int expectedTier, string expectedVariant)
    {
        var display = BadgeCatalogue.Resolve(key);

        display.Should().NotBeNull();
        display!.Tier.Should().Be(expectedTier);
        display.Variant.Should().Be(expectedVariant);
        display.MaxTier.Should().Be(3);
        display.GroupKey.Should().Be(BadgeGroupKeys.Marksman);
    }

    [Fact]
    public void Resolve_ShouldReturnGreen_ForAOneOffBadge()
    {
        var display = BadgeCatalogue.Resolve(BadgeKeys.Founder);

        display.Should().NotBeNull();
        display!.Variant.Should().Be("green");
        display.Tier.Should().Be(1);
        display.MaxTier.Should().Be(1);
    }

    [Fact]
    public void Resolve_ShouldReturnGold_ForAnHonour()
    {
        var honourKey = BadgeCatalogue.Groups
            .First(g => g.Category == BadgeCatalogue.HonourCategory)
            .Tiers[0].Key;

        var display = BadgeCatalogue.Resolve(honourKey);

        display.Should().NotBeNull();
        display!.Variant.Should().Be("gold");
    }

    [Fact]
    public void Resolve_ShouldCarryTheGroupNameAndGlyph()
    {
        var display = BadgeCatalogue.Resolve(BadgeKeys.Sharpshooter2);

        display.Should().NotBeNull();
        display!.Name.Should().Be("Sharpshooter");
        display.Glyph.Should().Be("crosshair");
        display.Key.Should().Be(BadgeKeys.Sharpshooter2);
    }

    [Fact]
    public void Resolve_ShouldHandleEveryKeyInTheCatalogue()
    {
        var allKeys = BadgeCatalogue.Groups.SelectMany(g => g.Tiers.Select(t => t.Key));

        foreach (var key in allKeys)
            BadgeCatalogue.Resolve(key).Should().NotBeNull($"'{key}' is declared in the catalogue");
    }

    // ---------- BuildCollection ----------

    [Fact]
    public void BuildCollection_ShouldBeLocked_WhenNothingIsEarnedAndTheMetricIsZero()
    {
        var dto = Group(State(), BadgeGroupKeys.Marksman);

        dto.State.Should().Be("Locked");
        dto.Tier.Should().Be(0);
        dto.MaxTier.Should().Be(3);
        dto.Progress.Should().Be(0d);
        dto.ProgressLabel.Should().Be("0 / 5");
    }

    [Fact]
    public void BuildCollection_ShouldBeInProgress_WhenTheMetricHasMovedButNoTierIsEarned()
    {
        var dto = Group(State(metrics: Metrics(seasonExactTotal: 2)), BadgeGroupKeys.Marksman);

        dto.State.Should().Be("InProgress");
        dto.Progress.Should().BeApproximately(2d / 5d, 0.0001);
        dto.ProgressLabel.Should().Be("2 / 5");
    }

    [Fact]
    public void BuildCollection_ShouldTrackTheNextThreshold_WhenALowerTierIsEarned()
    {
        var state = State(Earned((BadgeKeys.Marksman1, NowUtc.AddDays(-1))), Metrics(seasonExactTotal: 7));

        var dto = Group(state, BadgeGroupKeys.Marksman);

        dto.Tier.Should().Be(1);
        dto.State.Should().Be("InProgress");
        dto.ProgressLabel.Should().Be("7 / 10");
        dto.Progress.Should().BeApproximately(7d / 10d, 0.0001);
    }

    [Fact]
    public void BuildCollection_ShouldBeEarnedAndShowTheBest_WhenEveryTierIsEarned()
    {
        var state = State(
            Earned(
                (BadgeKeys.Marksman1, NowUtc.AddDays(-5)),
                (BadgeKeys.Marksman2, NowUtc.AddDays(-3)),
                (BadgeKeys.Marksman3, NowUtc.AddDays(-1))),
            Metrics(seasonExactTotal: 21));

        var dto = Group(state, BadgeGroupKeys.Marksman);

        dto.State.Should().Be("Earned");
        dto.Tier.Should().Be(3);
        dto.Progress.Should().Be(1d);
        dto.ProgressLabel.Should().Be("Best 21");
        dto.LastAwardedUtc.Should().Be(NowUtc.AddDays(-1));
    }

    [Fact]
    public void BuildCollection_ShouldCapProgressAtOne_WhenTheMetricExceedsTheNextThreshold()
    {
        var dto = Group(State(metrics: Metrics(seasonExactTotal: 99)), BadgeGroupKeys.Marksman);

        dto.Progress.Should().Be(1d);
    }

    [Fact]
    public void BuildCollection_ShouldReportNoLastAwarded_WhenNoTierIsEarned()
    {
        var dto = Group(State(), BadgeGroupKeys.Socialite);

        dto.LastAwardedUtc.Should().BeNull();
    }

    [Fact]
    public void BuildCollection_ShouldShowTheCurrentRun_ForOnFire()
    {
        var dto = Group(State(metrics: Metrics(bestStreak: 4, currentStreak: 2)), BadgeGroupKeys.OnFire);

        dto.SecondaryLabel.Should().Be("On a 2-round run");
    }

    [Fact]
    public void BuildCollection_ShouldSayThereIsNoRun_ForOnFireWithoutACurrentStreak()
    {
        var dto = Group(State(metrics: Metrics(bestStreak: 4)), BadgeGroupKeys.OnFire);

        dto.SecondaryLabel.Should().Be("No current run");
    }

    [Fact]
    public void BuildCollection_ShouldLeaveTheSecondaryLabelEmpty_ForOtherCollections()
    {
        var dto = Group(State(metrics: Metrics(leaguesJoined: 2)), BadgeGroupKeys.Socialite);

        dto.SecondaryLabel.Should().BeEmpty();
    }

    [Theory]
    [InlineData(BadgeGroupKeys.Marksman, 6, 0, 0, 0)]
    [InlineData(BadgeGroupKeys.Sharpshooter, 0, 6, 0, 0)]
    [InlineData(BadgeGroupKeys.OnFire, 0, 0, 6, 0)]
    [InlineData(BadgeGroupKeys.Socialite, 0, 0, 0, 6)]
    public void BuildCollection_ShouldReadEachGroupsOwnMetric(string groupKey, int seasonExact, int bestInRound, int bestStreak, int leaguesJoined)
    {
        var state = State(metrics: Metrics(seasonExact, bestInRound, bestStreak, leaguesJoined: leaguesJoined));

        var dto = Group(state, groupKey);

        dto.ProgressLabel.Should().StartWith("6 ").And.NotBe("0 / 0");
    }

    // ---------- BuildSingle ----------

    [Fact]
    public void BuildSingle_ShouldBeLocked_WhenNotEarned()
    {
        var dto = Group(State(), BadgeKeys.Founder);

        dto.State.Should().Be("Locked");
        dto.ProgressLabel.Should().Be("Locked");
        dto.Tier.Should().Be(0);
        dto.MaxTier.Should().Be(1);
        dto.Progress.Should().Be(0d);
        dto.Count.Should().Be(0);
        dto.LastAwardedUtc.Should().BeNull();
    }

    [Fact]
    public void BuildSingle_ShouldBeEarned_WhenAwarded()
    {
        var awardedUtc = NowUtc.AddDays(-2);
        var earned = new Dictionary<string, EarnedBadge>
        {
            [BadgeKeys.Founder] = new(BadgeKeys.Founder, 3, awardedUtc, null)
        };

        var dto = Group(State(earned), BadgeKeys.Founder);

        dto.State.Should().Be("Earned");
        dto.ProgressLabel.Should().Be("Earned");
        dto.Tier.Should().Be(1);
        dto.Progress.Should().Be(1d);
        dto.Count.Should().Be(3);
        dto.LastAwardedUtc.Should().Be(awardedUtc);
    }

    // ---------- BuildEverPresentProgress ----------

    [Fact]
    public void EverPresent_ShouldBeLocked_WhenThereIsNoProgressRecorded()
    {
        var dto = Group(State(), BadgeKeys.EverPresent);

        dto.State.Should().Be("Locked");
        dto.ProgressLabel.Should().Be("Locked");
        dto.Progress.Should().Be(0d);
    }

    [Fact]
    public void EverPresent_ShouldBeLocked_WhenTheSeasonHasNoRounds()
    {
        var state = State(metrics: Metrics(everPresent: new EverPresentProgress(0, 0, false)));

        var dto = Group(state, BadgeKeys.EverPresent);

        dto.State.Should().Be("Locked");
        dto.ProgressLabel.Should().Be("Locked");
    }

    [Fact]
    public void EverPresent_ShouldBeOnTrack_WhenNoRoundHasBeenMissed()
    {
        var state = State(metrics: Metrics(everPresent: new EverPresentProgress(7, 10, false)));

        var dto = Group(state, BadgeKeys.EverPresent);

        dto.State.Should().Be("InProgress");
        dto.ProgressLabel.Should().Be("On track - round 7 of 10");
        dto.Progress.Should().BeApproximately(0.7d, 0.0001);
    }

    [Fact]
    public void EverPresent_ShouldBeLockedAndShowTheBest_WhenARoundWasMissed()
    {
        var state = State(metrics: Metrics(everPresent: new EverPresentProgress(4, 10, true)));

        var dto = Group(state, BadgeKeys.EverPresent);

        dto.State.Should().Be("Locked");
        dto.ProgressLabel.Should().Be("Missed - best 4 of 10");
        dto.Progress.Should().BeApproximately(0.4d, 0.0001);
    }

    [Fact]
    public void EverPresent_ShouldUseTheEarnedPath_OnceAwarded()
    {
        var earned = Earned((BadgeKeys.EverPresent, NowUtc.AddDays(-1)));
        var state = State(earned, Metrics(everPresent: new EverPresentProgress(10, 10, false)));

        var dto = Group(state, BadgeKeys.EverPresent);

        dto.State.Should().Be("Earned");
        dto.ProgressLabel.Should().Be("Earned");
    }

    // ---------- BuildPage ----------

    [Fact]
    public void BuildPage_ShouldSplitTheCatalogueIntoItsThreeCategories()
    {
        var page = BadgeCatalogue.BuildPage(State(), NowUtc);

        page.Collections.Should().OnlyContain(d => d.Category == BadgeCatalogue.CollectionCategory);
        page.Badges.Should().OnlyContain(d => d.Category == BadgeCatalogue.BadgeCategory);
        page.Honours.Should().OnlyContain(d => d.Category == BadgeCatalogue.HonourCategory);

        (page.Collections.Count + page.Badges.Count + page.Honours.Count)
            .Should().Be(BadgeCatalogue.Groups.Count);
    }

    [Fact]
    public void BuildPage_ShouldReportTheEarnedAndTotalCounts()
    {
        var state = State(Earned(
            (BadgeKeys.Marksman1, NowUtc.AddDays(-2)),
            (BadgeKeys.Founder, NowUtc.AddDays(-1))));

        var page = BadgeCatalogue.BuildPage(state, NowUtc);

        page.EarnedCount.Should().Be(2);
        page.TotalCount.Should().Be(BadgeCatalogue.TotalBadgeCount);
    }

    // ---------- BuildTile ----------

    [Fact]
    public void BuildTile_ShouldPutARecentlyEarnedBadgeFirst()
    {
        var state = State(Earned((BadgeKeys.Founder, NowUtc.AddDays(-1))));

        var tile = BadgeCatalogue.BuildTile(state, NowUtc);

        tile.Carousel[0].Key.Should().Be(BadgeKeys.Founder);
    }

    [Fact]
    public void BuildTile_ShouldStillPutAnEarnedBadgeFirst_WhenTheAwardIsOlderThanTheRecentWindow()
    {
        // The "recently earned" flag is only the first sort key; the second orders by award date
        // across the whole list, so any earned badge outranks every unearned one either way.
        var state = State(Earned((BadgeKeys.Founder, NowUtc.AddDays(-30))));

        var tile = BadgeCatalogue.BuildTile(state, NowUtc);

        tile.Carousel[0].Key.Should().Be(BadgeKeys.Founder);
    }

    [Fact]
    public void BuildTile_ShouldOrderRecentAwardsNewestFirst()
    {
        var state = State(Earned(
            (BadgeKeys.Founder, NowUtc.AddDays(-5)),
            (BadgeKeys.OnCall, NowUtc.AddDays(-1))));

        var tile = BadgeCatalogue.BuildTile(state, NowUtc);

        tile.Carousel[0].Key.Should().Be(BadgeKeys.OnCall);
        tile.Carousel[1].Key.Should().Be(BadgeKeys.Founder);
    }

    [Fact]
    public void BuildTile_ShouldPreferTheBadgeClosestToItsNextTier_AmongUnearnedOnes()
    {
        var state = State(metrics: Metrics(seasonExactTotal: 4, leaguesJoined: 0));

        var tile = BadgeCatalogue.BuildTile(state, NowUtc);

        var keys = tile.Carousel.Select(b => b.Key).ToList();

        keys.IndexOf(BadgeGroupKeys.Marksman).Should().BeLessThan(keys.IndexOf(BadgeGroupKeys.Socialite));
    }

    [Fact]
    public void BuildTile_ShouldIncludeEveryGroupAndTheHeadlineCounts()
    {
        var state = State(Earned((BadgeKeys.Marksman1, NowUtc.AddDays(-2))));

        var tile = BadgeCatalogue.BuildTile(state, NowUtc);

        tile.Carousel.Should().HaveCount(BadgeCatalogue.Groups.Count);
        tile.EarnedCount.Should().Be(1);
        tile.TotalCount.Should().Be(BadgeCatalogue.TotalBadgeCount);
    }
}
