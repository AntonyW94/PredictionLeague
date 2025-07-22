namespace PredictionLeague.Application.Data;

public interface IApplicationReadDbConnection
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null);
}