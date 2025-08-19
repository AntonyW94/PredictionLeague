namespace PredictionLeague.Application.Data;

public interface IApplicationReadDbConnection
{
    #region Query Multiple 
    
    Task<IEnumerable<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken, object? param = null);

    #endregion

    #region Query Single
    
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken, object? param = null);

    #endregion
}