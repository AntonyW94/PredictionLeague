using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Badges;

/// <summary>
/// A single badge as shown in the UI. A "collection" (e.g. Marksman) is one BadgeDto whose
/// <see cref="Tier"/> is the highest tier reached and whose <see cref="Progress"/> tracks the next
/// tier; one-offs and honours use MaxTier 1. Progress values are computed live, never stored.
/// </summary>
[ExcludeFromCodeCoverage]
public record BadgeDto(
    string Key,
    string Name,
    string Description,
    string Glyph,
    string Category,
    string State,
    int Tier,
    int MaxTier,
    IReadOnlyList<int> Thresholds,
    double Progress,
    string ProgressLabel,
    int Count,
    DateTime? LastAwardedUtc)
{
    /// <summary>Optional second progress line, e.g. On Fire's live "current run" alongside the best.</summary>
    public string SecondaryLabel { get; init; } = string.Empty;
}
