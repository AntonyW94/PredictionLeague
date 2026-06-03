using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// A registry row describing a prize category: how it is scored (<see cref="Kind"/>), its default
/// weight when recommending a per-entry split, which competition types it is available for, whether
/// it pays ranked places, and its display name.
/// </summary>
public sealed record PrizeCategoryDefinition(
    PrizeType Category,
    PrizeCategoryKind Kind,
    int DefaultWeight,
    CategoryAvailability AvailableFor,
    bool IsRanked,
    string DisplayName);
