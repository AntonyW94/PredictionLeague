using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
using PredictionLeague.Shared.Admin.Leagues;
using PredictionLeague.Shared.Dashboard;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class LeagueRepository : ILeagueRepository
{
    private readonly string _connectionString;

    public LeagueRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection Connection => new SqlConnection(_connectionString);

    public async Task AddMemberAsync(int leagueId, string userId)
    {
        using var dbConnection = Connection;
        const string sql = "INSERT INTO LeagueMembers (LeagueId, UserId) VALUES (@LeagueId, @UserId);";
       
        await dbConnection.ExecuteAsync(sql, new { LeagueId = leagueId, UserId = userId });
    }

    public async Task CreateAsync(League league)
    {
        using var dbConnection = Connection;
        const string sql = @"
                INSERT INTO Leagues (Name, SeasonId, AdministratorUserId, EntryCode, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@Name, @SeasonId, @AdministratorUserId, @EntryCode, GETDATE());";

        var newId = await dbConnection.QuerySingleAsync<int>(sql, league);
        league.Id = newId;
    }

    public async Task<League?> GetByEntryCodeAsync(string entryCode)
    {
        using var dbConnection = Connection;
        const string sql = "SELECT * FROM Leagues WHERE EntryCode = @EntryCode;";
        
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
        using var dbConnection = Connection;
        const string sql = "SELECT * FROM LeagueMembers WHERE LeagueId = @LeagueId;";
        
        return await dbConnection.QueryAsync<LeagueMember>(sql, new { LeagueId = leagueId });
    }

    public async Task<IEnumerable<League>> GetLeaguesByUserIdAsync(string userId)
    {
        using var connection = Connection;
        const string sql = @"
            SELECT 
                l.* FROM [Leagues] l
            INNER JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            WHERE lm.[UserId] = @UserId;";
        return await connection.QueryAsync<League>(sql, new { UserId = userId });
    }

    public async Task<League?> GetDefaultPublicLeagueAsync()
    {
        using var connection = Connection;
        // For now, we assume the first public league created is the default.
        // In a real app, you might add an "IsDefault" flag to the Leagues table.
        const string sql = "SELECT TOP 1 * FROM [Leagues] WHERE [EntryCode] IS NULL ORDER BY [Id];";
        return await connection.QuerySingleOrDefaultAsync<League>(sql);
    }

    public async Task<IEnumerable<PublicLeagueDto>> GetAllPublicLeaguesForUserAsync(string userId)
    {
        using var connection = Connection;
        const string sql = @"
            SELECT 
                l.[Id],
                l.[Name],
                s.[Name] AS SeasonName,
                CASE WHEN lm.[UserId] IS NOT NULL THEN 1 ELSE 0 END AS IsMember
            FROM [Leagues] l
            INNER JOIN [Seasons] s ON l.[SeasonId] = s.[Id]
            LEFT JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId] AND lm.[UserId] = @UserId
            WHERE l.[EntryCode] IS NULL;";
        return await connection.QueryAsync<PublicLeagueDto>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<LeagueDto>> GetAllAsync()
    {
        using var connection = Connection;
        const string sql = @"
        SELECT 
            l.[Id],
            l.[Name],
            s.[Name] AS SeasonName,
            ISNULL(l.[EntryCode], 'Public') AS EntryCode,
            COUNT(lm.[UserId]) AS MemberCount
        FROM [Leagues] l
        INNER JOIN [Seasons] s ON l.[SeasonId] = s.[Id]
        LEFT JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
        GROUP BY l.[Id], l.[Name], s.[Name], l.[EntryCode]
        ORDER BY l.[Id];";
        return await connection.QueryAsync<LeagueDto>(sql);
    }

    public async Task UpdateAsync(League league)
    {
        using var connection = Connection;
        const string sql = @"
        UPDATE [Leagues] SET
            [Name] = @Name,
            [EntryCode] = @EntryCode
        WHERE [Id] = @Id;";
        await connection.ExecuteAsync(sql, league);
    }
}