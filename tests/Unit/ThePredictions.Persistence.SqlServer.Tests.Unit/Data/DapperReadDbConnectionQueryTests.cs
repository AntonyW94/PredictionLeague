using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Unit.Data;

/// <summary>
/// Exercises the read path against a real (in-memory) database rather than a mocked connection, so
/// the Dapper call, the retry wrapper and the slow-query timing are all genuinely run.
/// </summary>
public sealed class DapperReadDbConnectionQueryTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly IDbConnectionFactory _connectionFactory = Substitute.For<IDbConnectionFactory>();
    private readonly ILogger<DapperReadDbConnection> _logger = Substitute.For<ILogger<DapperReadDbConnection>>();

    // A shared in-memory database lives only while at least one connection is open.
    private const string ConnectionString = "Data Source=readpath;Mode=Memory;Cache=Shared";

    public DapperReadDbConnectionQueryTests()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        using var setup = _keepAlive.CreateCommand();
        setup.CommandText = "CREATE TABLE Leagues (Id INTEGER PRIMARY KEY, Name TEXT); " +
                            "INSERT INTO Leagues (Id, Name) VALUES (1, 'Alpha'), (2, 'Beta');";
        setup.ExecuteNonQuery();

        _connectionFactory.CreateConnection().Returns(_ =>
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return connection;
        });
    }

    public void Dispose() => _keepAlive.Dispose();

    private DapperReadDbConnection BuildConnection(
        int slowQueryThresholdMilliseconds = 500,
        IReadIsolationPolicy? isolationPolicy = null) =>
        new(_connectionFactory,
            new PassThroughRetryPolicy(),
            isolationPolicy ?? new UnwrappedIsolationPolicy(),
            Options.Create(new TimeoutSettings()),
            Options.Create(new QueryMonitoringSettings { SlowQueryThresholdMilliseconds = slowQueryThresholdMilliseconds }),
            _logger);

    private int WarningCount() => _logger.ReceivedCalls()
        .Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log)
                    && (LogLevel)c.GetArguments()[0]! == LogLevel.Warning);

    [Fact]
    public async Task QueryAsync_ShouldReturnEveryMatchingRow()
    {
        var names = await BuildConnection().QueryAsync<string>("SELECT Name FROM Leagues ORDER BY Id", CancellationToken.None);

        names.Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnNothing_WhenNoRowsMatch()
    {
        var names = await BuildConnection().QueryAsync<string>("SELECT Name FROM Leagues WHERE Id = 99", CancellationToken.None);

        names.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_ShouldPassParametersThrough()
    {
        var names = await BuildConnection().QueryAsync<string>(
            "SELECT Name FROM Leagues WHERE Id = @Id", CancellationToken.None, new { Id = 2 });

        names.Should().ContainSingle().Which.Should().Be("Beta");
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_ShouldReturnTheRow()
    {
        var name = await BuildConnection().QuerySingleOrDefaultAsync<string>(
            "SELECT Name FROM Leagues WHERE Id = @Id", CancellationToken.None, new { Id = 1 });

        name.Should().Be("Alpha");
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_ShouldReturnDefault_WhenThereIsNoRow()
    {
        var name = await BuildConnection().QuerySingleOrDefaultAsync<string>(
            "SELECT Name FROM Leagues WHERE Id = 99", CancellationToken.None);

        name.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_ShouldOpenTheConnection_WhenTheFactoryHandsBackAClosedOne()
    {
        // The real factory returns a closed SqlConnection - opening it is what the connection-cost
        // half of the slow-query warning measures, so the read has to work without Dapper doing it.
        var closedConnectionFactory = Substitute.For<IDbConnectionFactory>();
        closedConnectionFactory.CreateConnection().Returns(_ => new SqliteConnection(ConnectionString));

        var readDbConnection = new DapperReadDbConnection(
            closedConnectionFactory,
            new PassThroughRetryPolicy(),
            new UnwrappedIsolationPolicy(),
            Options.Create(new TimeoutSettings()),
            Options.Create(new QueryMonitoringSettings()),
            _logger);

        var names = await readDbConnection.QueryAsync<string>("SELECT Name FROM Leagues ORDER BY Id", CancellationToken.None);

        names.Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public async Task QueryAsync_ShouldExecuteTheSqlThePolicyReturns()
    {
        // The isolation policy rewrites the batch that actually runs, so a read that ignored its output
        // would run at whatever level the pooled connection happened to be left on.
        var names = await BuildConnection(isolationPolicy: new FixedSqlIsolationPolicy("SELECT Name FROM Leagues WHERE Id = 2"))
            .QueryAsync<string>("SELECT Name FROM Leagues ORDER BY Id", CancellationToken.None);

        names.Should().ContainSingle().Which.Should().Be("Beta");
    }

    [Fact]
    public async Task QueryAsync_ShouldNotWarn_WhenTheQueryIsComfortablyUnderTheThreshold()
    {
        await BuildConnection(slowQueryThresholdMilliseconds: 60_000)
            .QueryAsync<string>("SELECT Name FROM Leagues", CancellationToken.None);

        WarningCount().Should().Be(0);
    }

    [Fact]
    public async Task QueryAsync_ShouldWarn_WhenTheQueryMeetsTheSlowThreshold()
    {
        // Threshold zero makes every query count as slow, which is the only way to exercise the
        // warning deterministically.
        await BuildConnection(slowQueryThresholdMilliseconds: 0)
            .QueryAsync<string>("SELECT Name FROM Leagues", CancellationToken.None);

        WarningCount().Should().Be(1);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_ShouldWarn_WhenTheQueryMeetsTheSlowThreshold()
    {
        await BuildConnection(slowQueryThresholdMilliseconds: 0)
            .QuerySingleOrDefaultAsync<string>("SELECT Name FROM Leagues WHERE Id = 1", CancellationToken.None);

        WarningCount().Should().Be(1);
    }

    private sealed class PassThroughRetryPolicy : ISqlRetryPolicy
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);
    }

    /// <summary>
    /// The production policy wraps every read in <c>SET TRANSACTION ISOLATION LEVEL</c>, which the in-memory
    /// database backing these tests cannot parse. It is exercised directly by
    /// <c>ReadUncommittedIsolationPolicyTests</c>; here what matters is that the read path runs whatever the
    /// policy returns, which <see cref="QueryAsync_ShouldExecuteTheSqlThePolicyReturns"/> pins.
    /// </summary>
    private sealed class UnwrappedIsolationPolicy : IReadIsolationPolicy
    {
        public string Apply(string sql) => sql;
    }

    private sealed class FixedSqlIsolationPolicy(string sql) : IReadIsolationPolicy
    {
        public string Apply(string _) => sql;
    }
}
