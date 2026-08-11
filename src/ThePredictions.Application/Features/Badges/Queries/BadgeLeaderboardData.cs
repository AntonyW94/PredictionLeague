using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>What <see cref="IBadgeLeaderboardQuery"/> returns: the accounts, and the awards, unjoined.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BadgeLeaderboardData(
    IReadOnlyList<BadgePlayerRow> Players,
    IReadOnlyList<BadgePlayerAwardRow> Awards);
