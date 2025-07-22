using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public RefreshTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Create
    public async Task CreateAsync(RefreshToken token)
    {
        await Connection.ExecuteAsync("INSERT INTO RefreshTokens (UserId, Token, Expires, Created) VALUES (@UserId, @Token, @Expires, @Created)", token);
    }

    #endregion

    #region Read

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await Connection.QuerySingleOrDefaultAsync<RefreshToken>("SELECT * FROM RefreshTokens WHERE Token = @Token", new { Token = token });
    }

    #endregion

    #region Update

    public Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE [RefreshTokens] SET [Revoked] = GETUTCDATE() WHERE [UserId] = @UserId AND [Revoked] IS NULL;";

        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        return Connection.ExecuteAsync(command);
    }

    #endregion
}