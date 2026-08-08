using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Application.Features.Badges;

/// <summary>
/// The badges are defined here in code (mirrors OnboardingStepRegistry). The database only stores
/// which badges a user has *earned*; everything shown in the UI - names, glyphs, tiers, thresholds and
/// live progress toward the next tier - is composed here from the earned rows plus live metrics. Adding
/// a badge is a code change with no migration.
/// </summary>
public static class BadgeCatalogue
{
    public const string CollectionCategory = "Collection";
    public const string BadgeCategory = "Badge";
    public const string HonourCategory = "Honour";

    internal static readonly IReadOnlyList<BadgeGroup> Groups =
    [
        // Collections (levelled)
        new(BadgeGroupKeys.Marksman, "Marksman", "Exact scores in a season", "target", CollectionCategory, BadgeScope.PerSeason,
            [new(BadgeKeys.Marksman1, 5), new(BadgeKeys.Marksman2, 10), new(BadgeKeys.Marksman3, 15)]),
        new(BadgeGroupKeys.Sharpshooter, "Sharpshooter", "Exact scores in one round", "crosshair", CollectionCategory, BadgeScope.PerRound,
            [new(BadgeKeys.Sharpshooter1, 3), new(BadgeKeys.Sharpshooter2, 4), new(BadgeKeys.Sharpshooter3, 5)]),
        new(BadgeGroupKeys.OnFire, "On Fire", "Rounds in a row with an exact score", "flame", CollectionCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.OnFire1, 3), new(BadgeKeys.OnFire2, 5), new(BadgeKeys.OnFire3, 7)]),
        new(BadgeGroupKeys.Socialite, "Socialite", "Leagues joined (all-time)", "network", CollectionCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.Socialite1, 1), new(BadgeKeys.Socialite2, 3), new(BadgeKeys.Socialite3, 5)]),

        // Badges (one-offs)
        new(BadgeKeys.OffTheMark, "Off the Mark", "Submit your first predictions", "flag", BadgeCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.OffTheMark, 0)]),
        new(BadgeKeys.FirstBlood, "First Blood", "Your first-ever exact score", "bullseye", BadgeCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.FirstBlood, 0)]),
        new(BadgeKeys.OnTheBoard, "On the Board", "First round you score points in", "scoreboard", BadgeCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.OnTheBoard, 0)]),
        new(BadgeKeys.BeatTheCrowd, "Beat the Crowd", "Back the minority result and win", "crowd", BadgeCategory, BadgeScope.PerRound,
            [new(BadgeKeys.BeatTheCrowd, 0)]),
        new(BadgeKeys.EverPresent, "Ever-Present", "Predict every match of a full season", "calendar", BadgeCategory, BadgeScope.PerSeason,
            [new(BadgeKeys.EverPresent, 0)]),
        new(BadgeKeys.OnCall, "On Call", "Add a mobile number", "phone", BadgeCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.OnCall, 0)]),
        new(BadgeKeys.Banked, "Banked", "Add your bank details", "wallet", BadgeCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.Banked, 0)]),
        new(BadgeKeys.Founder, "Founder", "Create your own league", "shield", BadgeCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.Founder, 0)]),
        new(BadgeKeys.Veteran, "Veteran", "Return for a second season", "clock", BadgeCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.Veteran, 0)]),

        // Honours (placings)
        new(BadgeKeys.Champion, "Champion", "Win a league", "trophy", HonourCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.Champion, 0)]),
        new(BadgeKeys.Podium, "Podium", "Finish top 3 in a league", "podium", HonourCategory, BadgeScope.Lifetime,
            [new(BadgeKeys.Podium, 0)]),
        new(BadgeKeys.RoundWinner, "Round Winner", "Finish 1st in a round", "rosette", HonourCategory, BadgeScope.PerRound,
            [new(BadgeKeys.RoundWinner, 0)]),
        new(BadgeKeys.MonthWinner, "Month Winner", "Top a league for a calendar month", "calendar-star", HonourCategory, BadgeScope.PerRound,
            [new(BadgeKeys.MonthWinner, 0)]),
        new(BadgeKeys.StageWinner, "Stage Winner", "Win a stage of a tournament", "bracket", HonourCategory, BadgeScope.PerRound,
            [new(BadgeKeys.StageWinner, 0)])
    ];

    public static readonly int TotalBadgeCount = Groups.Sum(g => g.Tiers.Count);

    /// <summary>
    /// Resolves a badge key (including a collection tier key such as "sharpshooter-2") to its
    /// display metadata - name, glyph and the colour variant to render. Returns null for an
    /// unknown key. The variant mirrors <c>BadgeIcon.razor</c>: honours are gold, collection tiers
    /// step bronze -> silver -> gold, and one-off badges are green.
    /// </summary>
    public static BadgeDisplay? Resolve(string key)
    {
        foreach (var group in Groups)
        {
            var tier = TierOf(group, key);
            if (tier is null)
                continue;

            var variant = VariantFor(group.Category, tier.Value);

            return new BadgeDisplay(key, group.GroupKey, group.Name, group.Glyph, variant, tier.Value, group.Tiers.Count);
        }

        return null;
    }

    /// <summary>The 1-based tier this key represents within the group, or null if it is not in it.</summary>
    private static int? TierOf(BadgeGroup group, string key)
    {
        for (var i = 0; i < group.Tiers.Count; i++)
        {
            if (group.Tiers[i].Key == key)
                return i + 1;
        }

        return null;
    }

    /// <summary>
    /// The colour variant to render, mirroring <c>BadgeIcon.razor</c>: honours are gold, collection
    /// tiers step bronze -> silver -> gold, and one-off badges are green.
    /// </summary>
    private static string VariantFor(string category, int tier) => category switch
    {
        HonourCategory => "gold",
        // tier is 1 or more, so bronze is the floor and a fourth arm here would be unreachable.
        CollectionCategory => tier switch
        {
            >= 3 => "gold",
            2 => "silver",
            _ => "bronze"
        },
        _ => "green"
    };

    internal static UserBadgesDto BuildPage(BadgeUserState state, DateTime nowUtc)
    {
        var dtos = Groups.Select(g => ToDto(g, state, nowUtc)).ToList();

        return new UserBadgesDto(
            EarnedCount: state.Earned.Count,
            TotalCount: TotalBadgeCount,
            Collections: dtos.Where(d => d.Category == CollectionCategory).ToList(),
            Badges: dtos.Where(d => d.Category == BadgeCategory).ToList(),
            Honours: dtos.Where(d => d.Category == HonourCategory).ToList());
    }

    internal static BadgesTileDto BuildTile(BadgeUserState state, DateTime nowUtc)
    {
        var recentCutoff = nowUtc.AddDays(-10);

        var carousel = Groups
            .Select(g => ToDto(g, state, nowUtc))
            .OrderByDescending(d => d.LastAwardedUtc >= recentCutoff)        // recently earned first
            .ThenByDescending(d => d.LastAwardedUtc ?? DateTime.MinValue)    // newest of those
            .ThenByDescending(d => d.State != "Earned")                     // then still-to-earn
            .ThenByDescending(d => d.Progress)                              // closest to next
            .ToList();

        return new BadgesTileDto(state.Earned.Count, TotalBadgeCount, carousel);
    }

    private static BadgeDto ToDto(BadgeGroup group, BadgeUserState state, DateTime nowUtc)
    {
        return group.Category == CollectionCategory
            ? BuildCollection(group, state)
            : BuildSingle(group, state, nowUtc);
    }

    private static BadgeDto BuildCollection(BadgeGroup group, BadgeUserState state)
    {
        var tier = group.Tiers.Count(t => state.Earned.ContainsKey(t.Key));
        var maxTier = group.Tiers.Count;
        var maxed = tier == maxTier;
        var metric = MetricFor(group.GroupKey, state.Metrics);
        var nextThreshold = maxed ? 0 : group.Tiers[tier].Threshold;
        var lastAwarded = LastAwarded(group, state);

        return new BadgeDto(group.GroupKey, group.Name, group.Description, group.Glyph, group.Category,
            CollectionState(tier, metric, maxed),
            tier, maxTier,
            group.Tiers.Select(t => t.Threshold).ToList(),
            CollectionProgress(metric, nextThreshold, maxed),
            maxed ? $"Best {metric}" : $"{metric} / {nextThreshold}",
            0,
            lastAwarded == default ? null : lastAwarded)
        {
            SecondaryLabel = SecondaryLabelFor(group, state)
        };
    }

    /// <summary>How far round the ring goes: full once every tier is earned, otherwise the share of
    /// the next threshold reached.</summary>
    private static double CollectionProgress(int metric, int nextThreshold, bool maxed)
    {
        if (maxed)
            return 1d;

        // Not maxed means there is a next tier, and every catalogue threshold is positive.
        return Math.Min(1d, metric / (double)nextThreshold);
    }

    private static string CollectionState(int tier, int metric, bool maxed)
    {
        if (maxed)
            return "Earned";

        return tier > 0 || metric > 0 ? "InProgress" : "Locked";
    }

    private static DateTime LastAwarded(BadgeGroup group, BadgeUserState state) =>
        group.Tiers
            .Where(t => state.Earned.ContainsKey(t.Key))
            .Select(t => state.Earned[t.Key].LastAwardedUtc)
            .DefaultIfEmpty(default)
            .Max();

    /// <summary>
    /// On Fire shows the live current run beneath the best, since a streak is dynamic (it can drop
    /// to zero). The best drives the ring; the current run is informational. No other collection
    /// has a second line.
    /// </summary>
    private static string SecondaryLabelFor(BadgeGroup group, BadgeUserState state)
    {
        if (group.GroupKey != BadgeGroupKeys.OnFire)
            return string.Empty;

        return state.Metrics.CurrentStreak > 0
            ? $"On a {state.Metrics.CurrentStreak}-round run"
            : "No current run";
    }

    private static BadgeDto BuildSingle(BadgeGroup group, BadgeUserState state, DateTime nowUtc)
    {
        var key = group.GroupKey;

        if (state.Earned.TryGetValue(key, out var award))
            return new BadgeDto(key, group.Name, group.Description, group.Glyph, group.Category,
                "Earned", 1, 1, [], 1d, "Earned", award.Count, award.LastAwardedUtc);

        // Ever-Present is the one badge that shows how far through the season the player is,
        // rather than sitting flatly locked until it lands.
        if (key == BadgeKeys.EverPresent)
            return BuildEverPresentProgress(group, state);

        return new BadgeDto(key, group.Name, group.Description, group.Glyph, group.Category,
            "Locked", 0, 1, [], 0d, "Locked", 0, null);
    }

    private static BadgeDto BuildEverPresentProgress(BadgeGroup group, BadgeUserState state)
    {
        var ep = state.Metrics.EverPresent;

        if (ep is null || ep.RoundsTotal == 0)
            return new BadgeDto(group.GroupKey, group.Name, group.Description, group.Glyph, group.Category,
                "Locked", 0, 1, [], 0d, "Locked", 0, null);

        var progress = Math.Min(1d, ep.RoundsPredicted / (double)ep.RoundsTotal);

        var (stateWord, label) = ep.Missed
            ? ("Locked", $"Missed - best {ep.RoundsPredicted} of {ep.RoundsTotal}")
            : ("InProgress", $"On track - round {ep.RoundsPredicted} of {ep.RoundsTotal}");

        return new BadgeDto(group.GroupKey, group.Name, group.Description, group.Glyph, group.Category,
            stateWord, 0, 1, [], progress, label, 0, null);
    }

    // Only ever called for the four levelled collections; Socialite is the default arm because a
    // switch expression always needs one.
    private static int MetricFor(string groupKey, BadgeProgressMetrics m) => groupKey switch
    {
        BadgeGroupKeys.Marksman => m.SeasonExactTotal,
        BadgeGroupKeys.Sharpshooter => m.BestExactsInRound,
        BadgeGroupKeys.OnFire => m.BestStreak,
        _ => m.LeaguesJoined
    };
}

internal enum BadgeScope
{
    Lifetime,
    PerRound,
    PerSeason
}

internal static class BadgeGroupKeys
{
    public const string Marksman = "marksman";
    public const string Sharpshooter = "sharpshooter";
    public const string OnFire = "on-fire";
    public const string Socialite = "socialite";
}

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
internal record BadgeTier(string Key, int Threshold);

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
internal record BadgeGroup(
    string GroupKey,
    string Name,
    string Description,
    string Glyph,
    string Category,
    BadgeScope Scope,
    IReadOnlyList<BadgeTier> Tiers);

internal record EarnedBadge(string BadgeKey, int Count, DateTime LastAwardedUtc, string? Detail);

internal sealed record EverPresentProgress(int RoundsPredicted, int RoundsTotal, bool Missed);

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
internal sealed record BadgeProgressMetrics(
    int SeasonExactTotal,
    int BestExactsInRound,
    int BestStreak,
    int CurrentStreak,
    int LeaguesJoined,
    EverPresentProgress? EverPresent);

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
internal sealed record BadgeUserState(
    IReadOnlyDictionary<string, EarnedBadge> Earned,
    BadgeProgressMetrics Metrics);
