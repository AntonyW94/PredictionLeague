using FluentAssertions;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Common.Exceptions;

public class SeasonPassRequiredExceptionTests
{
    [Fact]
    public void Constructor_ShouldFormatMessageAndExposeSeasonId()
    {
        // Act
        var exception = new SeasonPassRequiredException(42);

        // Assert
        exception.SeasonId.Should().Be(42);
        exception.Message.Should().Be("A Season Pass is required to take part in season (ID: 42).");
    }

    [Fact]
    public void Constructor_ShouldBeAssignableToException()
    {
        // Act
        var exception = new SeasonPassRequiredException(1);

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }
}
