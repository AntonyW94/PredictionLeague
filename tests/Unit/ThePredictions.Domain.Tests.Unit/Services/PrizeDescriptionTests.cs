using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// How a prize is labelled - a three-armed <c>CASE</c> that ended in <c>DATENAME(MONTH, ...)</c>, and so in
/// whatever language the database login was configured with.
/// </summary>
public class PrizeDescriptionTests
{
    [Fact]
    public void For_ShouldPreferTheAdminsOwnWording()
    {
        PrizeDescription.For("1st Place", PrizeType.Round, roundNumber: 7, month: null)
            .Should().Be("1st Place");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void For_ShouldTreatBlankWordingAsNone(string? adminDescription)
    {
        // SQL Server ignores trailing spaces when comparing, so "   " counted as empty under the old <> ''.
        // IsNullOrEmpty would have changed the behaviour for that third case.
        PrizeDescription.For(adminDescription, PrizeType.Round, roundNumber: 7, month: null)
            .Should().Be("Round 7");
    }

    [Fact]
    public void For_ShouldNameTheMonth_ForAMonthlyPrize()
    {
        PrizeDescription.For(null, PrizeType.Monthly, roundNumber: null, month: 3)
            .Should().Be("March");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(null)]
    public void For_ShouldReturnNothing_ForAMonthlyPrizeWithAnImpossibleMonth(int? month)
    {
        // DATEFROMPARTS threw on this and took the whole tile down with it.
        PrizeDescription.For(null, PrizeType.Monthly, roundNumber: null, month)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(PrizeType.Overall)]
    [InlineData(PrizeType.MostExactScores)]
    [InlineData(PrizeType.Stages)]
    public void For_ShouldReturnNothing_ForAPrizeTypeThatNamesItself(PrizeType prizeType)
    {
        // The old CASE's ELSE NULL: an overall or exact-scores prize needs no derived label.
        PrizeDescription.For(null, prizeType, roundNumber: 7, month: 3).Should().BeNull();
    }
}
