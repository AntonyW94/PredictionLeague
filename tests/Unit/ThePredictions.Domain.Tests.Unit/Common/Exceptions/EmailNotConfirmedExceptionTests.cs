using FluentAssertions;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Common.Exceptions;

public class EmailNotConfirmedExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetMessage()
    {
        // Act
        var exception = new EmailNotConfirmedException();

        // Assert
        exception.Message.Should().Contain("confirm your email");
    }

    [Fact]
    public void Constructor_ShouldBeAssignableToException()
    {
        // Act
        var exception = new EmailNotConfirmedException();

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }
}
