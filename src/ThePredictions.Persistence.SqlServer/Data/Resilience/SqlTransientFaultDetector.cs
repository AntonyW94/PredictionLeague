using Microsoft.Data.SqlClient;

namespace ThePredictions.Persistence.SqlServer.Data.Resilience;

/// <summary>
/// Determines whether a SQL exception represents a transient fault that may
/// succeed on retry (e.g. connection drops, timeouts, deadlocks).
/// </summary>
public static class SqlTransientFaultDetector
{
    /// <summary>
    /// SQL Server error numbers that indicate a transient failure.
    /// </summary>
    private static readonly HashSet<int> TransientErrorNumbers =
    [
        // Timeout
        -2,

        // Server error during login / connection
        20,

        // Connection lost (named pipe / transport)
        64,

        // Connection closed by server
        233,

        // Deadlock victim
        1205,

        // Network-level errors
        10053, // Transport-level error (connection forcibly closed)
        10054, // Connection reset by remote host
        10060, // Connection timed out

        // Azure SQL / SQL Database specific
        40143, // Connection could not be initialised
        40197, // Service encountered an error processing request
        40501, // Service is busy
        40613, // Database not currently available
        49918, // Cannot process request (insufficient resources)
        49919, // Cannot process create or update request
        49920  // Cannot process request (too many operations)
    ];

    /// <summary>
    /// Returns <c>true</c> when the exception (or an inner exception) is a
    /// <see cref="SqlException"/> containing at least one transient error number.
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        if (exception is SqlException sqlException)
        {
            return ContainsTransientError(sqlException);
        }

        if (exception.InnerException is SqlException innerSqlException)
        {
            return ContainsTransientError(innerSqlException);
        }

        return false;
    }

    private static bool ContainsTransientError(SqlException sqlException)
    {
        foreach (SqlError error in sqlException.Errors)
        {
            if (TransientErrorNumbers.Contains(error.Number))
            {
                return true;
            }
        }

        return false;
    }
}
