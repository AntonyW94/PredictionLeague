using FluentAssertions;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Users.Queries;

/// <summary>
/// What a prize is called on the administrator's list of accounts.
///
/// Every value it reads is a stored one, and a single unexpected row must not take the screen down - so each case has a
/// fallback rather than a throw, and those fallbacks are most of what is tested here.
/// </summary>
public class UserPrizeTitleTests
{
    [Fact]
    public void Of_ShouldNameTheOverallPrize()
    {
        UserPrizeTitle.Of(PrizeType.Overall, stage: null, roundNumber: null, month: null)
            .Should().Be("Overall winner");
    }

    [Fact]
    public void Of_ShouldNameTheMostExactScoresPrize()
    {
        UserPrizeTitle.Of(PrizeType.MostExactScores, stage: null, roundNumber: null, month: null)
            .Should().Be("Most exact scores");
    }

    [Fact]
    public void Of_ShouldNumberARoundPrize()
    {
        UserPrizeTitle.Of(PrizeType.Round, stage: null, roundNumber: 21, month: null)
            .Should().Be("Round 21 winner");
    }

    [Fact]
    public void Of_ShouldNameARoundPrizeGenerically_WhenTheRoundNumberIsMissing()
    {
        // The column allows null, and a prize with no round is still a prize somebody won.
        UserPrizeTitle.Of(PrizeType.Round, stage: null, roundNumber: null, month: null)
            .Should().Be("Round winner");
    }

    [Fact]
    public void Of_ShouldNameTheMonthOfAMonthlyPrize()
    {
        UserPrizeTitle.Of(PrizeType.Monthly, stage: null, roundNumber: null, month: 11)
            .Should().Be("November winner");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(13)]
    public void Of_ShouldNameAMonthlyPrizeGenerically_WhenTheMonthCannotBeAMonth(int? month)
    {
        // The month is a stored integer with nothing stopping it being 0 or 13.
        UserPrizeTitle.Of(PrizeType.Monthly, stage: null, roundNumber: null, month: month)
            .Should().Be("Monthly winner");
    }

    [Fact]
    public void Of_ShouldNameTheStageOfATournamentPrize()
    {
        UserPrizeTitle.Of(PrizeType.Stages, stage: "Group stage", roundNumber: null, month: null)
            .Should().Be("Group stage winner");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Of_ShouldNameAStagePrizeGenerically_WhenThereIsNoStageName(string? stage)
    {
        UserPrizeTitle.Of(PrizeType.Stages, stage: stage, roundNumber: null, month: null)
            .Should().Be("Stage winner");
    }

    [Fact]
    public void Of_ShouldStillProduceALabel_ForAPrizeTypeItDoesNotRecognise()
    {
        // A value outside the enum is a state the column allows. An administrator looking at this screen is often looking
        // at it because something is wrong, so it has to render.
        UserPrizeTitle.Of((PrizeType)99, stage: null, roundNumber: null, month: null)
            .Should().Be("Prize");
    }
}
