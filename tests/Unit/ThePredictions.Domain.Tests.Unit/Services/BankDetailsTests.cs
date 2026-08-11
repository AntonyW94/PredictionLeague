using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// Whether a set of bank details can actually be paid to - the same rule for the league account members pay into and the
/// winner's account the administrator pays out to. Both handlers had their own copy.
/// </summary>
public class BankDetailsTests
{
    [Fact]
    public void AreComplete_ShouldBeTrue_WhenAllThreePartsArePresent()
    {
        BankDetails.AreComplete("A Lovelace", "00-00-00", "12345678").Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "00-00-00", "12345678")]
    [InlineData("A Lovelace", null, "12345678")]
    [InlineData("A Lovelace", "00-00-00", null)]
    public void AreComplete_ShouldBeFalse_WhenAnyPartIsMissing(
        string? accountName,
        string? sortCode,
        string? accountNumber)
    {
        // A half-filled account is no use to whoever is sending the money, and showing two of the three would invite
        // somebody to guess the rest.
        BankDetails.AreComplete(accountName, sortCode, accountNumber).Should().BeFalse();
    }

    [Fact]
    public void AreComplete_ShouldBeFalse_WhenNothingHasBeenGiven()
    {
        BankDetails.AreComplete(null, null, null).Should().BeFalse();
    }

    [Fact]
    public void AreComplete_ShouldBeTrue_ForBlankStrings()
    {
        // Deliberately only a null check, matching both handlers before this: an empty string is a stored value, and
        // treating it as missing would be a behaviour change rather than a move.
        BankDetails.AreComplete(string.Empty, string.Empty, string.Empty).Should().BeTrue();
    }
}
