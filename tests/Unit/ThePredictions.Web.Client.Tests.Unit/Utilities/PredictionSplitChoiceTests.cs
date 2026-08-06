using FluentAssertions;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.Utilities;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Utilities;

public class PredictionSplitChoiceTests
{
    private const string UserId = "user-1";
    private const int MatchId = 42;

    private static List<PredictionResultDto> Results(string userId, params PredictionScoreDto[] predictions) =>
    [
        new() { UserId = userId, Predictions = predictions.ToList() }
    ];

    private static PredictionScoreDto Prediction(int matchId, int? home, int? away) =>
        new(matchId, home, away, PredictionOutcome.Pending, false);

    [Fact]
    public void For_ShouldReturnH_WhenTheUserPredictedAHomeWin()
    {
        var results = Results(UserId, Prediction(MatchId, 3, 1));

        PredictionSplitChoice.For(results, UserId, MatchId).Should().Be("H");
    }

    [Fact]
    public void For_ShouldReturnA_WhenTheUserPredictedAnAwayWin()
    {
        var results = Results(UserId, Prediction(MatchId, 0, 2));

        PredictionSplitChoice.For(results, UserId, MatchId).Should().Be("A");
    }

    [Fact]
    public void For_ShouldReturnD_WhenTheUserPredictedADraw()
    {
        var results = Results(UserId, Prediction(MatchId, 1, 1));

        PredictionSplitChoice.For(results, UserId, MatchId).Should().Be("D");
    }

    [Fact]
    public void For_ShouldReturnD_ForAGoallessDraw()
    {
        var results = Results(UserId, Prediction(MatchId, 0, 0));

        PredictionSplitChoice.For(results, UserId, MatchId).Should().Be("D");
    }

    [Fact]
    public void For_ShouldReturnNull_WhenThereAreNoResults()
    {
        PredictionSplitChoice.For(null, UserId, MatchId).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void For_ShouldReturnNull_WhenThereIsNoSignedInUser(string? userId)
    {
        var results = Results(UserId, Prediction(MatchId, 2, 0));

        PredictionSplitChoice.For(results, userId, MatchId).Should().BeNull();
    }

    [Fact]
    public void For_ShouldReturnNull_WhenTheUserHasNoRowInTheResults()
    {
        var results = Results("someone-else", Prediction(MatchId, 2, 0));

        PredictionSplitChoice.For(results, UserId, MatchId).Should().BeNull();
    }

    [Fact]
    public void For_ShouldReturnNull_WhenTheUserHasNotPredictedThisMatch()
    {
        var results = Results(UserId, Prediction(MatchId + 1, 2, 0));

        PredictionSplitChoice.For(results, UserId, MatchId).Should().BeNull();
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    public void For_ShouldReturnNull_WhenEitherScoreIsMissing(int? home, int? away)
    {
        var results = Results(UserId, Prediction(MatchId, home, away));

        PredictionSplitChoice.For(results, UserId, MatchId).Should().BeNull();
    }

    [Fact]
    public void For_ShouldPickTheSignedInUsersRow_WhenSeveralPlayersArePresent()
    {
        List<PredictionResultDto> results =
        [
            new() { UserId = "other-1", Predictions = [Prediction(MatchId, 0, 3)] },
            new() { UserId = UserId, Predictions = [Prediction(MatchId, 3, 0)] },
            new() { UserId = "other-2", Predictions = [Prediction(MatchId, 1, 1)] }
        ];

        PredictionSplitChoice.For(results, UserId, MatchId).Should().Be("H");
    }

    [Fact]
    public void For_ShouldReturnNull_WhenTheResultsAreEmpty()
    {
        PredictionSplitChoice.For([], UserId, MatchId).Should().BeNull();
    }
}
