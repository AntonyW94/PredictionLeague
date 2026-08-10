using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Persistence.SqlServer.Data.Resilience;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Integration.Harness;

/// <summary>
/// Base for every test in this assembly. Wipes the database before each test and hands the test the
/// same seams the application is wired with, so what runs is the production code path rather than a
/// test-only imitation of it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTestBase : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;

    protected DatabaseTestBase(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
        ConnectionFactory = new TestDbConnectionFactory(fixture.ConnectionString);
        Seed = new TestDataSeeder(ConnectionFactory);
    }

    internal string ConnectionString => _fixture.ConnectionString;

    internal IDbConnectionFactory ConnectionFactory { get; }

    internal TestDataSeeder Seed { get; }

    /// <summary>
    /// The real <see cref="DapperReadDbConnection"/> over the test container - the seam every query
    /// handler takes, and the one handler unit tests substitute, which is why the SQL behind it has
    /// never run under test. Retry and slow-query logging are left at their production defaults; the
    /// logger is silent because a passing query has nothing to report.
    /// </summary>
    internal IApplicationReadDbConnection ReadDbConnection => new DapperReadDbConnection(
        ConnectionFactory,
        new SqlRetryPolicy(Options.Create(new SqlRetryPolicyOptions()), NullLogger<SqlRetryPolicy>.Instance),
        Options.Create(new TimeoutSettings()),
        Options.Create(new QueryMonitoringSettings()),
        NullLogger<DapperReadDbConnection>.Instance);

    /// <summary>
    /// A fresh transaction context per test. Repositories take this and fall back to a new connection
    /// while no transaction is active, which is how they behave outside <c>TransactionBehaviour</c>.
    /// </summary>
    internal IDbTransactionContext NewTransactionContext() => new DbTransactionContext(ConnectionFactory);

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Reads state back for assertions. Deliberately raw SQL rather than a repository: a test that
    /// asserts through the code under test can be fooled by that code.
    /// </summary>
    internal async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null)
    {
        using var connection = ConnectionFactory.CreateConnection();
        return (await connection.QueryAsync<T>(sql, parameters)).ToList();
    }

    internal async Task<T?> ScalarAsync<T>(string sql, object? parameters = null)
    {
        using var connection = ConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Runs a statement outside the code under test, for arranging a state the seeder does not cover or
    /// for demonstrating what the database does on its own.
    /// </summary>
    internal async Task ExecuteAsync(string sql, object? parameters = null)
    {
        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(sql, parameters);
    }
}
