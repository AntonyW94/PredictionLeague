using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace PredictionLeague.Infrastructure.Identity;

public class DapperRoleStore : IRoleStore<IdentityRole>
{
    private readonly string _connectionString;

    public DapperRoleStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection Connection => new SqlConnection(_connectionString);

    public async Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = @"
                INSERT INTO [AspNetRoles] 
                (
                    [Id], 
                    [Name], 
                    [NormalizedName], 
                    [ConcurrencyStamp]
                )
                VALUES 
                (
                    @Id, 
                    @Name, 
                    @NormalizedName, 
                    @ConcurrencyStamp
                );";
        await connection.ExecuteAsync(sql, role);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = "DELETE FROM [AspNetRoles] WHERE [Id] = @Id;";
        await connection.ExecuteAsync(sql, new { role.Id });
        return IdentityResult.Success;
    }

    public async Task<IdentityRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = "SELECT * FROM [AspNetRoles] WHERE [Id] = @Id;";
        return await connection.QuerySingleOrDefaultAsync<IdentityRole>(sql, new { Id = roleId });
    }

    public async Task<IdentityRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = "SELECT * FROM [AspNetRoles] WHERE [NormalizedName] = @NormalizedName;";
        return await connection.QuerySingleOrDefaultAsync<IdentityRole>(sql, new { NormalizedName = normalizedRoleName });
    }

    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.NormalizedName);
    }

    public Task<string> GetRoleIdAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Id);
    }

    public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Name);
    }

    public Task SetNormalizedRoleNameAsync(IdentityRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }



    public Task SetRoleNameAsync(IdentityRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = @"
                UPDATE [AspNetRoles] SET
                    [Name] = @Name, 
                    [NormalizedName] = @NormalizedName, 
                    [ConcurrencyStamp] = @ConcurrencyStamp
                WHERE [Id] = @Id;";
        await connection.ExecuteAsync(sql, role);
        return IdentityResult.Success;
    }

    public void Dispose()
    {
    }
}