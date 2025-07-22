namespace PredictionLeague.Application.Data;

public interface IApplicationReadDbConnection
{
    #region Query Multiple 
    
    Task<IEnumerable<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken, object? param = null);
    Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TReturn>(string sql, CancellationToken cancellationToken, Func<TFirst, TSecond, TReturn> map, object? param = null, string splitOn = "Id");

    #endregion

    #region Query Single
    
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken, object? param = null);

    #endregion
}