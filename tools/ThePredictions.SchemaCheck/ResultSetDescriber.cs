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

    public async Task<(List<ResultColumn>? Columns, string? Error)> DescribeAsync(string sql, string declarations, CancellationToken cancellationToken)
    {
        var describable = InListPattern.Replace(sql, "IN (@${name})");

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sys.sp_describe_first_result_set @tsql = @t, @params = @p";
            command.Parameters.AddWithValue("@t", describable);
            command.Parameters.AddWithValue("@p", declarations);

            var columns = new List<ResultColumn>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var position = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var nameOrdinal = reader.GetOrdinal("name");
                var typeOrdinal = reader.GetOrdinal("system_type_name");

                var name = await reader.IsDBNullAsync(nameOrdinal, cancellationToken) ? string.Empty : reader.GetString(nameOrdinal);
                var sqlType = await reader.IsDBNullAsync(typeOrdinal, cancellationToken) ? string.Empty : reader.GetString(typeOrdinal);

                columns.Add(new ResultColumn(++position, name, sqlType, SqlTypeMap.ToClrType(sqlType)));
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
