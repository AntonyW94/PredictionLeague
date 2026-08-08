using FluentAssertions;
using ThePredictions.Application.Common.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Models;

/// <summary>
/// The success-or-errors answer from an account operation. Callers read Errors without checking it
/// first, so it must never be null.
/// </summary>
public class UserManagerResultTests
{
    [Fact]
    public void Success_ShouldSucceedWithNoErrors()
    {
        var result = UserManagerResult.Success();

        result.Succeeded.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldCarryTheReasons()
    {
        var result = UserManagerResult.Failure(["Password too short", "Email already taken"]);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Equal("Password too short", "Email already taken");
    }

    [Fact]
    public void Failure_ShouldGiveAnEmptyListRatherThanNull_WhenNoReasonWasSupplied()
    {
        // Callers join Errors straight into a message, so a null here would throw instead of
        // reporting the failure.
        var result = UserManagerResult.Failure(null!);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().NotBeNull().And.BeEmpty();
    }
}
