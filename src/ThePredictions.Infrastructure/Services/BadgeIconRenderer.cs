using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;
using Svg.Skia;
using System.Text;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Badges;

namespace ThePredictions.Infrastructure.Services;

/// <summary>
/// Renders a badge's earned icon to a PNG for emails, mirroring the client badge face
/// (<c>BadgeIcon.razor</c>): a tinted disc, a full "earned" ring and the glyph, with the CSS-driven
/// colours baked in per variant. Composes the SVG and rasterises it with Svg.Skia (the share-card
/// stack); results are cached since badge art is stable.
/// </summary>
public class BadgeIconRenderer(IMemoryCache cache) : IBadgeIconRenderer
{
    private const int RenderSize = 240;         // 2x the 120-unit badge viewBox for crisp email icons
    private const string TrackColour = "#E4E1EA";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public byte[]? Render(string badgeKey)
    {
        var display = BadgeCatalogue.Resolve(badgeKey);
        if (display is null)
            return null;

        var cacheKey = $"badge-icon-png::{badgeKey}";
        if (cache.TryGetValue(cacheKey, out byte[]? cached))
            return cached;

        var png = Compose(display);
        cache.Set(cacheKey, png, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
        return png;
    }

    private static byte[] Compose(BadgeDisplay display)
    {
        var (discColour, discOpacity, accent) = PaletteFor(display.Variant);
        var glyph = BadgeGlyphs.Svg(display.Glyph).Replace("currentColor", accent);

        var svg =
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120">""" +
            $"""<circle cx="60" cy="60" r="40" fill="{discColour}" fill-opacity="{discOpacity}"/>""" +
            $"""<circle cx="60" cy="60" r="50" fill="none" stroke="{TrackColour}" stroke-width="8"/>""" +
            $"""<circle cx="60" cy="60" r="50" fill="none" stroke="{accent}" stroke-width="8" stroke-linecap="round"/>""" +
            $"""<g>{glyph}</g></svg>""";

        using var skSvg = new SKSvg();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        var picture = skSvg.Load(stream);

        using var surface = SKSurface.Create(new SKImageInfo(RenderSize, RenderSize));
        surface.Canvas.Clear(SKColors.Transparent);

        if (picture is not null)
        {
            var matrix = SKMatrix.CreateScale(RenderSize / 120f, RenderSize / 120f);
            surface.Canvas.DrawPicture(picture, in matrix);
            surface.Canvas.Flush();
        }

        using var image = surface.Snapshot();
        using var data = image.Encode();
        return data.ToArray();
    }

    // Baked light-theme values from tp-badges.css (email has no CSS/theme). Disc is a translucent tint;
    // the ring and glyph share the solid accent colour.
    private static (string DiscColour, string DiscOpacity, string Accent) PaletteFor(string variant) => variant switch
    {
        "gold" => ("#C9B037", "0.16", "#B7972A"),
        "silver" => ("#8F8F8F", "0.16", "#8F8F8F"),
        "bronze" => ("#6A3805", "0.12", "#6A3805"),
        _ => ("#00B960", "0.15", "#00B960")
    };
}
