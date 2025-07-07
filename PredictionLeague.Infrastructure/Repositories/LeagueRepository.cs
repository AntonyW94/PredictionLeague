using Dapper;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using PredictionLeague.Infrastructure.Data;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class LeagueRepository : ILeagueRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LeagueRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public async Task<IEnumerable<League>> GetAllAsync()
    {
        const string sql = "SELECT * FROM [Leagues];";

        using var connection = Connection;
        return await connection.QueryAsync<League>(sql);
    }

    public async Task<IEnumerable<League>> GetPublicLeaguesAsync()
    {
        const string sql = "SELECT * FROM [Leagues] WHERE [EntryCode] IS NULL;";

        using var connection = Connection;
        return await connection.QueryAsync<League>(sql);
    }

    public async Task AddMemberAsync(LeagueMember member)
    {
        const string sql = "INSERT INTO LeagueMembers (LeagueId, UserId, Status, JoinedAt) VALUES (@LeagueId, @UserId, @Status, @JoinedAt);";
       
        using var dbConnection = Connection;
        await dbConnection.ExecuteAsync(sql, member);
    }

    public async Task CreateAsync(League league)
    {
        const string sql = @"
                INSERT INTO Leagues (Name, SeasonId, AdministratorUserId, EntryCode, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@Name, @SeasonId, @AdministratorUserId, @EntryCode, GETDATE());";

        using var dbConnection = Connection;
        var newId = await dbConnection.QuerySingleAsync<int>(sql, league);
        league.Id = newId;
    }

    public async Task<League?> GetByEntryCodeAsync(string entryCode)
    {
        const string sql = "SELECT * FROM Leagues WHERE EntryCode = @EntryCode;";

        using var dbConnection = Connection;
        return await dbConnection.QuerySingleOrDefaultAsync<League>(sql, new { EntryCode = entryCode });
    }

    public async Task<League?> GetByIdAsync(int id)
    {
        using var dbConnection = Connection;
        const string sql = "SELECT * FROM Leagues WHERE Id = @Id;";
        return await dbConnection.QuerySingleOrDefaultAsync<League>(sql, new { Id = id });
    }

    public async Task<IEnumerable<LeagueMember>> GetMembersByLeagueIdAsync(int leagueId)
    {
        const string sql = "SELECT * FROM LeagueMembers WHERE LeagueId = @LeagueId;";

        using var dbConnection = Connection;
        return await dbConnection.QueryAsync<LeagueMember>(sql, new { LeagueId = leagueId });
    }

    public async Task<IEnumerable<League>> GetLeaguesByUserIdAsync(string userId)
    {
        const string sql = @"
                SELECT 
                    l.* FROM [Leagues] l
                INNER JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
                WHERE lm.[UserId] = @UserId;";

        using var connection = Connection;
        return await connection.QueryAsync<League>(sql, new { UserId = userId });
    }

    public async Task<League?> GetDefaultPublicLeagueAsync()
    {
        const string sql = "SELECT TOP 1 * FROM [Leagues] WHERE [EntryCode] IS NULL ORDER BY [Id];";

        using var connection = Connection;
        return await connection.QuerySingleOrDefaultAsync<League>(sql);
    }

    public async Task UpdateAsync(League league)
    {
        const string sql = @"
                UPDATE [Leagues] SET
                    [Name] = @Name,
                    [EntryCode] = @EntryCode
                WHERE [Id] = @Id;";

        using var connection = Connection;
        await connection.ExecuteAsync(sql, league);
    }

    public async Task UpdateMemberStatusAsync(int leagueId, string userId, string status)
    {
        const string sql = "UPDATE [LeagueMembers] SET [Status] = @Status WHERE [LeagueId] = @LeagueId AND [UserId] = @UserId;";
       
        using var connection = Connection;
        await connection.ExecuteAsync(sql, new { Status = status, LeagueId = leagueId, UserId = userId });
    }
}