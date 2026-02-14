using FluentAssertions;
using PredictionLeague.Domain.Services.Boosts;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Boosts;

public class BoostEligibilityEvaluatorTests
{
    private const bool DefaultEnabled = true;
    private const int DefaultTotalUses = 3;
    private const int DefaultSeasonUses = 0;
    private const int DefaultWindowUses = 0;
    private const bool DefaultHasUsedThisRound = false;
    private const int DefaultRoundNumber = 3;
    private const bool DefaultIsMember = true;
    private const bool DefaultIsRoundInSeason = true;

    #region Early-Exit Rejection Tests

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenRoundNotInLeagueSeason()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: false);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("season");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenUserIsNotMember()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: false,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("member");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenBoostIsDisabled()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: false,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("not enabled");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenTotalUsesPerSeasonIsZero()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 0,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenTotalUsesPerSeasonIsNegative()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: -1,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
    }

    #endregion

    #region Round-Level Rejection Tests

    [Fact]
    public void Evaluate_ShouldReturnAlreadyUsed_WhenHasUsedThisRound()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: true,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.AlreadyUsedThisRound.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenSeasonLimitReached()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 3,
            seasonUses: 3,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("Season limit");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenSeasonUsesExceedLimit()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 3,
            seasonUses: 5,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("Season limit");
    }

    #endregion

    #region Window-Based Rejection Tests

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenRoundNotInAnyWindow()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 15,
            windows: CreateWindows(1, 5, 2),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("not available");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenWindowMaxUsesIsZero()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 3,
            windows: CreateWindows(1, 5, 0),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("cannot be used");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenWindowMaxUsesIsNegative()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 3,
            windows: CreateWindows(1, 5, -1),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("cannot be used");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenWindowLimitReached()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: 2,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 3,
            windows: CreateWindows(1, 5, 2),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("Window limit");
    }

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenWindowUsesExceedLimit()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: 5,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 3,
            windows: CreateWindows(1, 5, 2),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("Window limit");
    }

    #endregion

    #region Success (Allowed) Tests

    [Fact]
    public void Evaluate_ShouldReturnAllowed_WhenNoWindowsDefined()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ShouldReturnAllowed_WhenEmptyWindowsList()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: new List<BoostWindowSnapshot>(),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ShouldReturnAllowed_WhenRoundIsInWindowAndLimitsNotReached()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: 1,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 3,
            windows: CreateWindows(1, 5, 2),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldReturnAllowed_WhenRoundIsAtWindowBoundaryStart()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 1,
            windows: CreateWindows(1, 5, 2),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldReturnAllowed_WhenRoundIsAtWindowBoundaryEnd()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 5,
            windows: CreateWindows(1, 5, 2),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldReturnAllowed_WhenRoundIsInSecondWindow()
    {
        // Arrange
        var windows = new List<BoostWindowSnapshot>
        {
            new() { StartRoundNumber = 1, EndRoundNumber = 5, MaxUsesInWindow = 2 },
            new() { StartRoundNumber = 10, EndRoundNumber = 15, MaxUsesInWindow = 3 }
        };

        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 11,
            windows: windows,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldReturnAllowed_WhenSeasonUsesLessThanLimit()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 3,
            seasonUses: 1,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeTrue();
    }

    #endregion

    #region Remaining Uses Calculation Tests

    [Fact]
    public void Evaluate_ShouldCalculateSeasonRemaining_WhenNoWindows()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 3,
            seasonUses: 1,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.RemainingSeasonUses.Should().Be(2);
    }

    [Fact]
    public void Evaluate_ShouldCalculateWindowRemaining_WhenWindowExists()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 5,
            seasonUses: 0,
            windowUses: 1,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 3,
            windows: CreateWindows(1, 5, 3),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.RemainingWindowUses.Should().Be(2);
    }

    [Fact]
    public void Evaluate_ShouldSetWindowRemainingToSeasonRemaining_WhenNoWindows()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 3,
            seasonUses: 1,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.RemainingWindowUses.Should().Be(2);
        result.RemainingSeasonUses.Should().Be(2);
    }

    [Fact]
    public void Evaluate_ShouldReturnCorrectRemaining_WhenWindowRemainingDiffersFromSeason()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: 5,
            seasonUses: 0,
            windowUses: 1,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: 3,
            windows: CreateWindows(1, 5, 2),
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.RemainingSeasonUses.Should().Be(5);
        result.RemainingWindowUses.Should().Be(1);
    }

    #endregion

    #region Result Object Property Tests

    [Fact]
    public void Evaluate_ShouldSetAlreadyUsedThisRoundTrue_WhenUsedThisRound()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: true,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.AlreadyUsedThisRound.Should().BeTrue();
        result.CanUse.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShouldSetAlreadyUsedThisRoundFalse_WhenNotUsedAndNotAllowed()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: false,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.AlreadyUsedThisRound.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShouldSetReasonToNull_WhenAllowed()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: DefaultEnabled,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ShouldSetRemainingToZero_WhenNotAllowed()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: false,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.RemainingSeasonUses.Should().Be(0);
        result.RemainingWindowUses.Should().Be(0);
    }

    #endregion

    #region Guard Evaluation Order Tests

    [Fact]
    public void Evaluate_ShouldCheckRoundInSeasonFirst_WhenMultipleConditionsFail()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: false,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: false);

        // Assert
        result.Reason.Should().Contain("season");
    }

    [Fact]
    public void Evaluate_ShouldCheckMembershipBeforeEnabled_WhenBothFail()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: false,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: false,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.Reason.Should().Contain("member");
    }

    [Fact]
    public void Evaluate_ShouldCheckEnabledBeforeUsesPerSeason_WhenBothFail()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: false,
            totalUsesPerSeason: 0,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.Reason.Should().Contain("not enabled");
    }

    #endregion

    #region Helpers

    private static List<BoostWindowSnapshot> CreateWindows(int start, int end, int maxUses) =>
    [
        new BoostWindowSnapshot
        {
            StartRoundNumber = start,
            EndRoundNumber = end,
            MaxUsesInWindow = maxUses
        }
    ];

    #endregion
}
