using FluentAssertions;
using ThePredictions.Contracts.Badges;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Badges;

public class BadgeGlyphsTests
{
    /// <summary>
    /// Every glyph name the badge catalogue asks for. A typo on either side silently falls back to
    /// a plain circle in the UI and the badge email, which is exactly what these tests catch.
    /// </summary>
    private static readonly string[] AllGlyphs =
    [
        "target", "crosshair", "bullseye", "network", "flame", "flag", "scoreboard", "crowd",
        "calendar", "rosette", "trophy", "podium", "phone", "wallet", "shield", "calendar-star",
        "bracket", "clock"
    ];

    public static TheoryData<string> KnownGlyphs => [.. AllGlyphs];

    private const string FallbackGlyph =
        """<circle cx="60" cy="60" r="16" fill="none" stroke="currentColor" stroke-width="3"/>""";

    [Theory]
    [MemberData(nameof(KnownGlyphs))]
    public void Svg_ShouldReturnDedicatedMarkup_ForAKnownGlyph(string glyph)
    {
        var svg = BadgeGlyphs.Svg(glyph);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().NotBe(FallbackGlyph, "'{0}' should have its own artwork, not the fallback", glyph);
    }

    [Theory]
    [MemberData(nameof(KnownGlyphs))]
    public void Svg_ShouldUseCurrentColour_SoTheGlyphCanBeThemed(string glyph)
    {
        // The client colours the glyph via CSS and the server bakes a colour in for email; both
        // rely on every glyph painting with currentColor rather than a hard-coded value.
        BadgeGlyphs.Svg(glyph).Should().Contain("currentColor");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-glyph")]
    [InlineData("TARGET")]
    [InlineData("Target")]
    public void Svg_ShouldFallBackToAPlainCircle_ForAnythingUnrecognised(string glyph)
    {
        BadgeGlyphs.Svg(glyph).Should().Be(FallbackGlyph);
    }

    [Fact]
    public void Svg_ShouldFallBackToAPlainCircle_ForNull()
    {
        BadgeGlyphs.Svg(null!).Should().Be(FallbackGlyph);
    }

    [Theory]
    [MemberData(nameof(KnownGlyphs))]
    public void Svg_ShouldReturnBalancedMarkup(string glyph)
    {
        var svg = BadgeGlyphs.Svg(glyph);

        svg.Count(c => c == '<').Should().Be(svg.Count(c => c == '>'));
    }

    [Fact]
    public void Svg_ShouldGiveEveryKnownGlyphDistinctArtwork()
    {
        var rendered = AllGlyphs.Select(BadgeGlyphs.Svg).ToList();

        rendered.Should().OnlyHaveUniqueItems();
    }
}
