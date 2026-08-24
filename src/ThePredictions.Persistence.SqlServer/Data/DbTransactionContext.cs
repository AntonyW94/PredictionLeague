using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using System.Data;

namespace ThePredictions.Persistence.SqlServer.Data;

[ExcludeFromCodeCoverage(Justification = "Database plumbing: connection, transaction and type-handler wiring with no branching logic of its own.")]
public class DbTransactionContext(IDbConnectionFactory connectionFactory) : IDbTransactionContext, IAsyncDisposable, IDisposable
{
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _begun;

    public bool HasActiveTransaction => _begun;

    public IDbConnection Connection
    {
        get
        {
            if (!_begun)
                throw new InvalidOperationException("No active transaction. Call BeginAsync first.");

            if (_connection == null)
            {
                _connection = connectionFactory.CreateConnection();
                _connection.Open();

                // The level is stated rather than inherited. A pooled connection carries its isolation
                // level back into the pool - measured against the live server: set it, close the
                // connection, and the same SPID comes back still holding it - and the query side
                // deliberately runs at READ UNCOMMITTED. A read that failed mid-batch before restoring
                // the level would otherwise hand this transaction a session that reads dirty, and a
                // command that reads before it writes would then be deciding on data that does not
                // exist. Naming the level here makes that impossible rather than unlikely, and costs
                // nothing: the provider sends it in the same round trip as BEGIN TRANSACTION.
                _transaction = _connection.BeginTransaction(IsolationLevel.ReadCommitted);
            }

            return _connection;
        }
    }

    public IDbTransaction Transaction
    {
        get
        {
            if (!_begun)
                throw new InvalidOperationException("No active transaction. Call BeginAsync first.");

            if (_transaction == null)
            {
                // Accessing Connection triggers lazy initialisation
                _ = Connection;
            }

            return _transaction!;
        }
    }

    public Task BeginAsync(CancellationToken cancellationToken)
    {
        if (_begun)
            throw new InvalidOperationException("A transaction is already active.");

        _begun = true;
        return Task.CompletedTask;
    }

    // Committing ends the transaction, so the context stops reporting one as active and lets go of the
    // connection. Without that, HasActiveTransaction stayed true for the rest of the request scope and
    // every repository call after a commit was handed the committed transaction object - which throws.
    // That is what made "do this after the writes are committed" impossible to express, and the round
    // completion work (prize settlement, badges, two email sends) stayed inside the scoring transaction
    // holding its locks as a result.
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction == null)
        {
            _begun = false;
            return Task.CompletedTask;
        }

        _transaction.Commit();
        Dispose();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
        _transaction = null;
        _connection = null;
        _begun = false;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
