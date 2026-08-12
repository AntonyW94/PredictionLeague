using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ThePredictions.SchemaCheck;

/// <summary>
/// Asks SQL Server for a statement's result-set shape. sp_describe_first_result_set is metadata only -
/// it compiles the statement without executing it - so this is safe to point at any database, including
/// one holding real data, and safe for INSERT/UPDATE statements with an OUTPUT clause.
/// </summary>
public sealed class ResultSetDescriber(string connectionString)
{
    // Dapper expands `IN @Ids` into a parenthesised list at execution time; SQL Server cannot compile the
    // unexpanded form, so it is parenthesised here purely to get the statement past the parser.
    private static readonly Regex InListPattern = new(@"\bIN\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Confirms the database is actually reachable. Without this an unreachable server would turn every
    /// read into a per-query "could not be described" skip and the run would exit 0 - a clean bill of
    /// health from a check that never ran, which is worse than no check at all.
    /// </summary>
    public async Task<string?> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return null;
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or ArgumentException)
        {
            return exception.Message.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        }
    }

    public async Task<(List<ResultColumn>? Columns, string? Error)> DescribeAsync(string sql, string declarations, CancellationToken cancellationToken)
    {
        var describable = InListPattern.Replace(sql, "IN (@${name})");

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            // Browse mode 1 fills in source_schema/source_table/source_column. Without it they come back null for every
            // column, including plain references to one - which makes it impossible to tell a real column from an
            // expression, and so impossible to trust is_nullable.
            command.CommandText = "EXEC sys.sp_describe_first_result_set @tsql = @t, @params = @p, @browse_information_mode = 1";
            command.Parameters.AddWithValue("@t", describable);
            command.Parameters.AddWithValue("@p", declarations);

            var columns = new List<ResultColumn>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var position = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                // Browse mode appends the key columns a client would need to identify a row. They are not part of the
                // result the statement returns, and counting them would make every read look like it had spare columns.
                var hiddenOrdinal = reader.GetOrdinal("is_hidden");
                if (!await reader.IsDBNullAsync(hiddenOrdinal, cancellationToken) && reader.GetBoolean(hiddenOrdinal))
                    continue;

                var nameOrdinal = reader.GetOrdinal("name");
                var typeOrdinal = reader.GetOrdinal("system_type_name");
                var nullableOrdinal = reader.GetOrdinal("is_nullable");
                var sourceColumnOrdinal = reader.GetOrdinal("source_column");

                var name = await reader.IsDBNullAsync(nameOrdinal, cancellationToken) ? string.Empty : reader.GetString(nameOrdinal);
                var sqlType = await reader.IsDBNullAsync(typeOrdinal, cancellationToken) ? string.Empty : reader.GetString(typeOrdinal);

                // is_nullable is a bit, and true for anything the server cannot prove non-null. source_column is set only
                // where the column is a plain reference to one, which is how an expression is told from a real column.
                var isNullable = !await reader.IsDBNullAsync(nullableOrdinal, cancellationToken)
                                 && reader.GetBoolean(nullableOrdinal);

                var fromTableColumn = !await reader.IsDBNullAsync(sourceColumnOrdinal, cancellationToken);

                columns.Add(new ResultColumn(
                    ++position, name, sqlType, SqlTypeMap.ToClrType(sqlType), isNullable, fromTableColumn));
            }

            return columns.Count == 0
                ? (null, "SQL Server described no columns for this statement")
                : (columns, null);
        }
        catch (SqlException exception)
        {
            return (null, exception.Message.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal));
        }
    }
}
