using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// The one definition of how a player's name is shown to other players, replacing seventeen copies of
/// <c>FirstName + ' ' + LEFT(LastName, 1)</c> written into SQL across the Application layer.
/// </summary>
public class PlayerDisplayNameTests
{
    [Theory]
    [InlineData("Ada", "Lovelace", "Ada L")]
    [InlineData("Grace", "Hopper", "Grace H")]
    [InlineData("Ada", "l", "Ada l")]
    public void Format_ShouldReturnFirstNameAndLastInitial_WhenBothArePresent(string first, string last, string expected)
    {
        PlayerDisplayName.Format(first, last).Should().Be(expected);
    }

    [Theory]
    [InlineData("Ada", "", "Ada")]
    [InlineData("Ada", "   ", "Ada")]
    [InlineData("Ada", null, "Ada")]
    public void Format_ShouldReturnTheFirstNameAlone_WhenThereIsNoLastName(string first, string? last, string expected)
    {
        // The SQL produced "Ada " here, with a trailing space. The schema forbids a null name, so this is
        // defensive rather than a case in the data - but a trailing space is never what was wanted.
        PlayerDisplayName.Format(first, last).Should().Be(expected);
    }

    [Fact]
    public void Format_ShouldTrimBothParts_WhenTheyCarrySurroundingWhitespace()
    {
        PlayerDisplayName.Format("  Ada  ", "  Lovelace  ").Should().Be("Ada L");
    }

    [Theory]
    [InlineData(null, null, "")]
    [InlineData("", "", "")]
    [InlineData(null, "Lovelace", "L")]
    public void Format_ShouldNotThrow_WhenNamesAreMissingEntirely(string? first, string? last, string expected)
    {
        PlayerDisplayName.Format(first, last).Should().Be(expected);
    }
}
