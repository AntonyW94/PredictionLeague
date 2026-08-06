using FluentAssertions;
using ThePredictions.Infrastructure.Data.Resilience;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Data.Resilience;

/// <summary>
/// Decides whether a failed database call is worth retrying. Too narrow and a routine Azure SQL
/// failover surfaces as an error to the user; too wide and a genuine bug is retried until it times
/// out.
/// </summary>
public class SqlTransientFaultDetectorTests
{
    [Theory]
    [InlineData(-2)]      // timeout
    [InlineData(20)]      // error during login
    [InlineData(64)]      // connection lost
    [InlineData(233)]     // connection closed by server
    [InlineData(1205)]    // deadlock victim
    [InlineData(10053)]   // transport-level error
    [InlineData(10054)]   // connection reset
    [InlineData(10060)]   // connection timed out
    [InlineData(40143)]   // Azure: connection could not be initialised
    [InlineData(40197)]   // Azure: service error processing request
    [InlineData(40501)]   // Azure: service busy
    [InlineData(40613)]   // Azure: database not currently available
    public void IsTransient_ShouldBeTrue_ForAKnownTransientErrorNumber(int errorNumber)
    {
        var exception = SqlExceptionFactory.WithErrorNumbers(errorNumber);

        SqlTransientFaultDetector.IsTransient(exception).Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]        // generic
    [InlineData(2627)]     // unique constraint violation - retrying will never help
    [InlineData(547)]      // foreign key violation
    [InlineData(8152)]     // string or binary data would be truncated
    public void IsTransient_ShouldBeFalse_ForAPermanentErrorNumber(int errorNumber)
    {
        var exception = SqlExceptionFactory.WithErrorNumbers(errorNumber);

        SqlTransientFaultDetector.IsTransient(exception).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ShouldBeTrue_WhenAnyErrorInTheCollectionIsTransient()
    {
        // A SqlException can carry several errors; one retryable fault is enough.
        var exception = SqlExceptionFactory.WithErrorNumbers(2627, 1205);

        SqlTransientFaultDetector.IsTransient(exception).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_ShouldBeFalse_WhenEveryErrorIsPermanent()
    {
        var exception = SqlExceptionFactory.WithErrorNumbers(2627, 547);

        SqlTransientFaultDetector.IsTransient(exception).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ShouldLookInsideAWrappedException()
    {
        // Dapper and our own plumbing often wrap the driver's exception.
        var inner = SqlExceptionFactory.WithErrorNumbers(1205);
        var wrapped = new InvalidOperationException("Read failed.", inner);

        SqlTransientFaultDetector.IsTransient(wrapped).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_ShouldBeFalse_WhenTheWrappedExceptionIsPermanent()
    {
        var inner = SqlExceptionFactory.WithErrorNumbers(2627);
        var wrapped = new InvalidOperationException("Read failed.", inner);

        SqlTransientFaultDetector.IsTransient(wrapped).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ShouldBeFalse_ForAnExceptionThatIsNotFromSqlServer()
    {
        SqlTransientFaultDetector.IsTransient(new InvalidOperationException("Something else.")).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ShouldBeFalse_WhenTheInnerExceptionIsNotFromSqlServer()
    {
        var wrapped = new InvalidOperationException("Outer.", new TimeoutException("Inner."));

        SqlTransientFaultDetector.IsTransient(wrapped).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ShouldOnlyLookOneLevelDown()
    {
        // Documents the current depth: a transient fault buried two levels deep is not detected.
        var deepest = SqlExceptionFactory.WithErrorNumbers(1205);
        var middle = new InvalidOperationException("Middle.", deepest);
        var outer = new InvalidOperationException("Outer.", middle);

        SqlTransientFaultDetector.IsTransient(outer).Should().BeFalse();
    }
}
