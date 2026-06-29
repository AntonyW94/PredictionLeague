using FluentAssertions;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.FootballApi.DTOs;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

public class UpdateScoresForNextRoundCommandHandlerTests
{
    private static readonly DateTime MatchTime = new(2026, 6, 29, 20, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("FT")]
    [InlineData("AET")]
    [InlineData("PEN")]
    public void GetMatchStatus_ShouldBeCompleted_WhenApiStatusIsFinal(string apiStatus)
    {
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus(apiStatus, isKnockout: false)
            .Should().Be(MatchStatus.Completed);
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus(apiStatus, isKnockout: true)
            .Should().Be(MatchStatus.Completed);
    }

    [Theory]
    [InlineData("1H")]
    [InlineData("2H")]
    [InlineData("HT")]
    [InlineData("LIVE")]
    public void GetMatchStatus_ShouldBeInProgress_WhenWithinRegulation(string apiStatus)
    {
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus(apiStatus, isKnockout: false)
            .Should().Be(MatchStatus.InProgress);
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus(apiStatus, isKnockout: true)
            .Should().Be(MatchStatus.InProgress);
    }

    [Theory]
    [InlineData("BT")]
    [InlineData("ET")]
    [InlineData("P")]
    public void GetMatchStatus_ShouldBeCompleted_WhenKnockoutIsPastRegulation(string apiStatus)
    {
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus(apiStatus, isKnockout: true)
            .Should().Be(MatchStatus.Completed);
    }

    [Theory]
    [InlineData("BT")]
    [InlineData("ET")]
    [InlineData("P")]
    public void GetMatchStatus_ShouldStayInProgress_WhenNonKnockoutIsPastRegulation(string apiStatus)
    {
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus(apiStatus, isKnockout: false)
            .Should().Be(MatchStatus.InProgress);
    }

    [Fact]
    public void GetMatchStatus_ShouldBePostponed_WhenApiStatusIsPostponed()
    {
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus("PST", isKnockout: true)
            .Should().Be(MatchStatus.Postponed);
    }

    [Theory]
    [InlineData("NS")]
    [InlineData("TBD")]
    [InlineData("")]
    public void GetMatchStatus_ShouldBeScheduled_WhenApiStatusIsUnrecognised(string apiStatus)
    {
        UpdateScoresForNextRoundCommandHandler.GetMatchStatus(apiStatus, isKnockout: true)
            .Should().Be(MatchStatus.Scheduled);
    }

    [Fact]
    public void GetScoreForMatch_ShouldReturnFullTimeScore_WhenKnockoutAndFullTimePresent()
    {
        // A knockout level at 1-1 after 90 with a goal in extra time: Goals shows the running
        // aggregate (2-1) but the scored result must stay the 90-minute FullTime score (1-1).
        var fixture = new FixtureResponse
        {
            Goals = new Goals { Home = 2, Away = 1 },
            Score = new Score { FullTime = new ScoreDetail { Home = 1, Away = 1 } }
        };

        UpdateScoresForNextRoundCommandHandler.GetScoreForMatch(fixture, isKnockout: true)
            .Should().Be((1, 1));
    }

    [Fact]
    public void GetScoreForMatch_ShouldFallBackToGoals_WhenKnockoutButFullTimeMissing()
    {
        var fixture = new FixtureResponse
        {
            Goals = new Goals { Home = 1, Away = 1 },
            Score = new Score { FullTime = null }
        };

        UpdateScoresForNextRoundCommandHandler.GetScoreForMatch(fixture, isKnockout: true)
            .Should().Be((1, 1));
    }

    [Fact]
    public void GetScoreForMatch_ShouldReturnGoals_WhenNotKnockout()
    {
        var fixture = new FixtureResponse
        {
            Goals = new Goals { Home = 3, Away = 0 },
            Score = new Score { FullTime = new ScoreDetail { Home = 1, Away = 1 } }
        };

        UpdateScoresForNextRoundCommandHandler.GetScoreForMatch(fixture, isKnockout: false)
            .Should().Be((3, 0));
    }

    [Fact]
    public void IsKnockoutMatch_ShouldBeFalse_WhenApiRoundNameMissing()
    {
        var match = Match.Create(roundId: 1, homeTeamId: 10, awayTeamId: 20, MatchTime, externalId: 100);

        UpdateScoresForNextRoundCommandHandler.IsKnockoutMatch(match).Should().BeFalse();
    }

    [Fact]
    public void IsKnockoutMatch_ShouldBeFalse_WhenGroupStage()
    {
        var match = Match.CreatePlaceholder(roundId: 1, "Team A", "Team B", "Group Stage - 1");

        UpdateScoresForNextRoundCommandHandler.IsKnockoutMatch(match).Should().BeFalse();
    }

    [Fact]
    public void IsKnockoutMatch_ShouldBeFalse_WhenRoundNameUnrecognised()
    {
        var match = Match.CreatePlaceholder(roundId: 1, "Team A", "Team B", "Friendly");

        UpdateScoresForNextRoundCommandHandler.IsKnockoutMatch(match).Should().BeFalse();
    }

    [Fact]
    public void IsKnockoutMatch_ShouldBeTrue_WhenKnockoutRoundName()
    {
        var match = Match.CreatePlaceholder(roundId: 1, "Team A", "Team B", "Round of 16");

        UpdateScoresForNextRoundCommandHandler.IsKnockoutMatch(match).Should().BeTrue();
    }
}
