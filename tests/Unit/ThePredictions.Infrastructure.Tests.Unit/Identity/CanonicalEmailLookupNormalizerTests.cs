using FluentAssertions;
using ThePredictions.Infrastructure.Identity;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Identity;

/// <summary>
/// This is what makes plus-aliases collide on the unique email index (ADR-0009), so it is the thing
/// stopping someone farming repeat free trials with you+1@, you+2@ and so on.
/// </summary>
public class CanonicalEmailLookupNormalizerTests
{
    private readonly CanonicalEmailLookupNormalizer _normaliser = new();

    [Fact]
    public void NormalizeEmail_ShouldUpperCaseForStorage()
    {
        _normaliser.NormalizeEmail("player@example.com").Should().Be("PLAYER@EXAMPLE.COM");
    }

    [Fact]
    public void NormalizeEmail_ShouldStripAPlusAlias()
    {
        _normaliser.NormalizeEmail("player+season2@example.com").Should().Be("PLAYER@EXAMPLE.COM");
    }

    [Fact]
    public void NormalizeEmail_ShouldCollapseEveryAliasOntoTheSameKey()
    {
        var first = _normaliser.NormalizeEmail("player+one@example.com");
        var second = _normaliser.NormalizeEmail("player+two@example.com");
        var plain = _normaliser.NormalizeEmail("PLAYER@example.com");

        first.Should().Be(second).And.Be(plain);
    }

    [Fact]
    public void NormalizeEmail_ShouldTrimSurroundingWhitespace()
    {
        _normaliser.NormalizeEmail("  player@example.com  ").Should().Be("PLAYER@EXAMPLE.COM");
    }

    [Fact]
    public void NormalizeEmail_ShouldNotTouchAValueWithoutAnAtSign()
    {
        _normaliser.NormalizeEmail("not-an-email").Should().Be("NOT-AN-EMAIL");
    }

    [Fact]
    public void NormalizeEmail_ShouldReturnNull_ForNull()
    {
        _normaliser.NormalizeEmail(null).Should().BeNull();
    }

    [Fact]
    public void NormalizeName_ShouldCanonicaliseTheSameWay()
    {
        // Usernames are emails in this application, so both normalise identically.
        _normaliser.NormalizeName("player+alias@example.com").Should().Be("PLAYER@EXAMPLE.COM");
    }

    [Fact]
    public void NormalizeName_ShouldReturnNull_ForNull()
    {
        _normaliser.NormalizeName(null).Should().BeNull();
    }
}
