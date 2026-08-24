using FluentAssertions;
using ThePredictions.Persistence.SqlServer.Data;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Unit.Data;

public class ReadUncommittedIsolationPolicyTests
{
    private readonly ReadUncommittedIsolationPolicy _policy = new();

    [Fact]
    public void Apply_ShouldRunTheReadAtReadUncommitted()
    {
        var batch = _policy.Apply("SELECT [Name] FROM [Leagues];");

        batch.Should().StartWith("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
    }

    [Fact]
    public void Apply_ShouldPutTheLevelBack()
    {
        // Without this the level rides the pooled connection into whatever runs next - confirmed against the
        // live server, where the same SPID comes back still set.
        var batch = _policy.Apply("SELECT [Name] FROM [Leagues];");

        batch.Should().EndWith("SET TRANSACTION ISOLATION LEVEL READ COMMITTED;");
    }

    [Fact]
    public void Apply_ShouldLeaveTheReadsOwnResultSetFirst()
    {
        // Dapper materialises the batch's first result set, so nothing that returns rows may precede the read.
        var batch = _policy.Apply("SELECT [Name] FROM [Leagues];");

        batch.Should().Contain("SELECT [Name] FROM [Leagues];");
        batch.IndexOf("SELECT", StringComparison.Ordinal)
            .Should().BeLessThan(batch.LastIndexOf("SET TRANSACTION", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SELECT 1;")]
    [InlineData("SELECT 1")]
    [InlineData("SELECT 1 -- a trailing line comment")]
    public void Apply_ShouldTerminateTheReadOnItsOwnLine_HoweverTheSqlEnds(string sql)
    {
        // The terminator cannot be appended to the read's last line: a query already ending in a semicolon
        // does not need one, and one ending in a line comment would swallow it. All three forms parse.
        var batch = _policy.Apply(sql);

        batch.Should().Contain($"{sql}\n;\n");
    }
}
