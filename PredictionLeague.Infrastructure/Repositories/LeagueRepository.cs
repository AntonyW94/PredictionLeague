using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using PredictionLeague.Core.Repositories;
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
}