using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>What <see cref="IBadgeStateQuery"/> returns.</summary>
/// <remarks>
/// The name is the player's own, kept here rather than fetched separately by the page: it is a first name and a
/// last initial, and that abbreviation is a rule, so what arrives are the two raw parts.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BadgeStateData(
    string? OwnerFirstName,
    string? OwnerLastName,
    IReadOnlyList<BadgeAwardRow> Awards,
    IReadOnlyList<BadgeRoundRow> Rounds,
    int LeaguesJoined);
