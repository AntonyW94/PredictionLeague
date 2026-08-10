using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.Conformance;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;

/// <summary>
/// Base for every test in this assembly. Wipes the database before each test and hands the test the
/// same seams the application is wired with, so what runs is the production code path rather than a
/// test-only imitation of it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTestBase : IAsyncLifetime
{
    private readonly SqlServerTestHarness _harness;

    protected DatabaseTestBase(SqlServerDatabaseFixture fixture)
    {
        _harness = new SqlServerTestHarness(fixture);
    }

    internal string ConnectionString => _harness.ConnectionString;

    internal IDbConnectionFactory ConnectionFactory => _harness.ConnectionFactory;

    internal ITestDataSeeder Seed => _harness.Seed;

    internal IApplicationReadDbConnection ReadDbConnection => _harness.ReadDbConnection;

    /// <summary>
    /// A fresh transaction context per test. Repositories take this and fall back to a new connection
    /// while no transaction is active, which is how they behave outside <c>TransactionBehaviour</c>.
    /// </summary>
    internal IDbTransactionContext NewTransactionContext() => _harness.NewTransactionContext();

    public async ValueTask InitializeAsync() => await _harness.ResetAsync();

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
