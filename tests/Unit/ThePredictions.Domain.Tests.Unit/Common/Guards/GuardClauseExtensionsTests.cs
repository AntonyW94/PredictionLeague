using FluentAssertions;
using PredictionLeague.Domain.Common.Exceptions;
using PredictionLeague.Domain.Common.Guards;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Common.Guards;

public class GuardClauseExtensionsTests
{
    [Fact]
    public void EntityNotFound_ShouldThrowEntityNotFoundException_WhenInputIsNull()
    {
        // Act
        var act = () => Ardalis.GuardClauses.Guard.Against.EntityNotFound<string>(42, null);

        // Assert
        act.Should().Throw<EntityNotFoundException>().WithMessage("*42*was not found*");
    }

    [Fact]
    public void EntityNotFound_ShouldNotThrow_WhenInputIsNotNull()
    {
        // Act
        var act = () => Ardalis.GuardClauses.Guard.Against.EntityNotFound(42, "some-value");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EntityNotFound_ShouldIncludeCustomName_WhenProvided()
    {
        // Act
        var act = () => Ardalis.GuardClauses.Guard.Against.EntityNotFound<string>(7, null, "League");

        // Assert
        act.Should().Throw<EntityNotFoundException>().WithMessage("League (ID: 7) was not found.");
    }

    [Fact]
    public void EntityNotFound_ShouldUseDefaultEntityName_WhenNameNotProvided()
    {
        // Act
        var act = () => Ardalis.GuardClauses.Guard.Against.EntityNotFound<string>(1, null);

        // Assert
        act.Should().Throw<EntityNotFoundException>().WithMessage("Entity (ID: 1) was not found.");
    }
}
