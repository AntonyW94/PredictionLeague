using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RefreshTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>("SELECT * FROM RefreshTokens WHERE Token = @Token", new { Token = token });
    }

    public async Task AddAsync(RefreshToken token)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("INSERT INTO RefreshTokens (UserId, Token, Expires, Created) VALUES (@UserId, @Token, @Expires, @Created)", token);
    }

    public async Task RevokeAsync(string token)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE RefreshTokens SET Revoked = @Revoked WHERE Token = @Token AND Revoked IS NULL", new { Revoked = DateTime.UtcNow, Token = token });
    }
}