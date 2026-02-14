using FluentAssertions;
using PredictionLeague.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Common.Exceptions;

public class EntityNotFoundExceptionTests
{
    [Fact]
    public void Constructor_ShouldFormatMessage_WhenNameAndKeyProvided()
    {
        // Act
        var exception = new EntityNotFoundException("League", 42);

        // Assert
        exception.Message.Should().Be("League (ID: 42) was not found.");
    }

    [Fact]
    public void Constructor_ShouldFormatMessage_WhenKeyIsString()
    {
        // Act
        var exception = new EntityNotFoundException("User", "abc-123");

        // Assert
        exception.Message.Should().Be("User (ID: abc-123) was not found.");
    }

    [Fact]
    public void Constructor_ShouldBeAssignableToException()
    {
        // Act
        var exception = new EntityNotFoundException("Team", 1);

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }
}
