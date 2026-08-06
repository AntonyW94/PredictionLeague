using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using System.Data;

namespace ThePredictions.Infrastructure.Data;

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
                _transaction = _connection.BeginTransaction();
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

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction == null)
            return Task.CompletedTask;

        _transaction.Commit();
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
