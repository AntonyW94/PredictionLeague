using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using System.Data;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public abstract class RepositoryBase(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
{
    protected IDbConnection Connection => transactionContext.HasActiveTransaction
        ? transactionContext.Connection
        : connectionFactory.CreateConnection();

    protected IDbTransaction? Transaction => transactionContext.HasActiveTransaction
        ? transactionContext.Transaction
        : null;
}
