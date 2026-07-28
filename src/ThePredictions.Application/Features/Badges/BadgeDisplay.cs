namespace ThePredictions.Application.Features.Badges;

/// <summary>
/// Display metadata for a single badge key, resolved from the catalogue: the group it belongs to,
/// its name and glyph, the colour variant to render (green / bronze / silver / gold) and the tier
/// it represents. Used to render the badge icon server-side and to label it in the digest email.
/// </summary>
public record BadgeDisplay(string Key, string GroupKey, string Name, string Glyph, string Variant, int Tier, int MaxTier);
