using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Core.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Identity
{
    public class DapperUserStore : IUserStore<ApplicationUser>, IUserPasswordStore<ApplicationUser>
    {
        private readonly string _connectionString;

        public DapperUserStore(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = Connection;
            const string sql = @"
                INSERT INTO [AspNetUsers] 
                (
                    [Id], 
                    [UserName], 
                    [NormalizedUserName], 
                    [Email], 
                    [NormalizedEmail], 
                    [EmailConfirmed], 
                    [PasswordHash], 
                    [SecurityStamp], 
                    [ConcurrencyStamp], 
                    [PhoneNumber], 
                    [PhoneNumberConfirmed], 
                    [TwoFactorEnabled], 
                    [LockoutEnd], 
                    [LockoutEnabled], 
                    [AccessFailedCount], 
                    [FirstName], 
                    [LastName]
                )
                VALUES 
                (
                    @Id, 
                    @UserName, 
                    @NormalizedUserName, 
                    @Email, 
                    @NormalizedEmail, 
                    @EmailConfirmed, 
                    @PasswordHash, 
                    @SecurityStamp, 
                    @ConcurrencyStamp, 
                    @PhoneNumber, 
                    @PhoneNumberConfirmed, 
                    @TwoFactorEnabled, 
                    @LockoutEnd, 
                    @LockoutEnabled, 
                    @AccessFailedCount, 
                    @FirstName, 
                    @LastName
                );";
            await connection.ExecuteAsync(sql, user);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = Connection;
            var sql = "DELETE FROM [AspNetUsers] WHERE [Id] = @Id;";
            await connection.ExecuteAsync(sql, new { user.Id });
            return IdentityResult.Success;
        }

        public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = Connection;
            var sql = "SELECT * FROM [AspNetUsers] WHERE [Id] = @Id;";
            return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { Id = userId });
        }

        public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = Connection;
            var sql = "SELECT * FROM [AspNetUsers] WHERE [NormalizedUserName] = @NormalizedUserName;";
            return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { NormalizedUserName = normalizedUserName });
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id);
        }

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = Connection;
            const string sql = @"
                UPDATE [AspNetUsers] SET
                    [UserName] = @UserName, 
                    [NormalizedUserName] = @NormalizedUserName, 
                    [Email] = @Email, 
                    [NormalizedEmail] = @NormalizedEmail, 
                    [EmailConfirmed] = @EmailConfirmed, 
                    [PasswordHash] = @PasswordHash, 
                    [SecurityStamp] = @SecurityStamp, 
                    [ConcurrencyStamp] = @ConcurrencyStamp, 
                    [PhoneNumber] = @PhoneNumber, 
                    [PhoneNumberConfirmed] = @PhoneNumberConfirmed, 
                    [TwoFactorEnabled] = @TwoFactorEnabled, 
                    [LockoutEnd] = @LockoutEnd, 
                    [LockoutEnabled] = @LockoutEnabled, 
                    [AccessFailedCount] = @AccessFailedCount, 
                    [FirstName] = @FirstName, 
                    [LastName] = @LastName
                WHERE [Id] = @Id;";
            await connection.ExecuteAsync(sql, user);
            return IdentityResult.Success;
        }

        public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.PasswordHash != null);
        }

        public void Dispose()
        {
            // Nothing to dispose.
        }
    }
}
