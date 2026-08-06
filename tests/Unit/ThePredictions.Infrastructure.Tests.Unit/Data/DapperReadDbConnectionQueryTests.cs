using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Infrastructure.Data;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Data;

/// <summary>
/// Exercises the read path against a real (in-memory) database rather than a mocked connection, so
/// the Dapper call, the retry wrapper and the slow-query timing are all genuinely run.
/// </summary>
public sealed class DapperReadDbConnectionQueryTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly IDbConnectionFactory _connectionFactory = Substitute.For<IDbConnectionFactory>();
    private readonly ILogger<DapperReadDbConnection> _logger = Substitute.For<ILogger<DapperReadDbConnection>>();

    public DapperReadDbConnectionQueryTests()
    {
        // A shared in-memory database lives only while at least one connection is open.
        const string connectionString = "Data Source=readpath;Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        using var setup = _keepAlive.CreateCommand();
        setup.CommandText = "CREATE TABLE Leagues (Id INTEGER PRIMARY KEY, Name TEXT); " +
                            "INSERT INTO Leagues (Id, Name) VALUES (1, 'Alpha'), (2, 'Beta');";
        setup.ExecuteNonQuery();

        _connectionFactory.CreateConnection().Returns(_ =>
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            return connection;
        });
    }

    public void Dispose() => _keepAlive.Dispose();

    private DapperReadDbConnection BuildConnection(int slowQueryThresholdMilliseconds = 500) =>
        new(_connectionFactory,
            new PassThroughRetryPolicy(),
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
}
