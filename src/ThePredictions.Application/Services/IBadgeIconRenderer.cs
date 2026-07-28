namespace ThePredictions.Application.Services;

/// <summary>
/// Renders a badge's earned icon to a PNG for use in emails (which cannot use the app's inline SVG
/// or CSS). Returns null for an unknown badge key.
/// </summary>
public interface IBadgeIconRenderer
{
    byte[]? Render(string badgeKey);
}
