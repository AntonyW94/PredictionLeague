﻿using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Application.Data;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Identity;

public class DapperUserStore : IUserPasswordStore<ApplicationUser>, IUserEmailStore<ApplicationUser>, IUserRoleStore<ApplicationUser>, IUserLoginStore<ApplicationUser>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperUserStore(IConfiguration configuration, IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private IDbConnection Connection => _connectionFactory.CreateConnection();

    #region IUserStore, IUserPasswordStore, IUserEmailStore Methods
    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = @"
                INSERT INTO [AspNetUsers] 
                (
                    [Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], 
                    [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], 
                    [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount], [FirstName], [LastName]
                )
                VALUES 
                (
                    @Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @EmailConfirmed, 
                    @PasswordHash, @SecurityStamp, @ConcurrencyStamp, @PhoneNumber, @PhoneNumberConfirmed, 
                    @TwoFactorEnabled, @LockoutEnd, @LockoutEnabled, @AccessFailedCount, @FirstName, @LastName
                );";
        await connection.ExecuteAsync(sql, user);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = "DELETE FROM [AspNetUsers] WHERE [Id] = @Id;";
        await connection.ExecuteAsync(sql, new { user.Id });
        return IdentityResult.Success;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = "SELECT * FROM [AspNetUsers] WHERE [Id] = @Id;";
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { Id = userId });
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = "SELECT * FROM [AspNetUsers] WHERE [NormalizedUserName] = @NormalizedUserName;";
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { NormalizedUserName = normalizedUserName });
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);

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
                    [UserName] = @UserName, [NormalizedUserName] = @NormalizedUserName, [Email] = @Email, 
                    [NormalizedEmail] = @NormalizedEmail, [EmailConfirmed] = @EmailConfirmed, [PasswordHash] = @PasswordHash, 
                    [SecurityStamp] = @SecurityStamp, [ConcurrencyStamp] = @ConcurrencyStamp, [PhoneNumber] = @PhoneNumber, 
                    [PhoneNumberConfirmed] = @PhoneNumberConfirmed, [TwoFactorEnabled] = @TwoFactorEnabled, [LockoutEnd] = @LockoutEnd, 
                    [LockoutEnabled] = @LockoutEnabled, [AccessFailedCount] = @AccessFailedCount, 
                    [FirstName] = @FirstName, [LastName] = @LastName
                WHERE [Id] = @Id;";
        await connection.ExecuteAsync(sql, user);
        return IdentityResult.Success;
    }

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash != null);
    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = "SELECT * FROM [AspNetUsers] WHERE [NormalizedEmail] = @NormalizedEmail;";
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { NormalizedEmail = normalizedEmail });
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public void Dispose() { }

    #endregion

    #region IUserRoleStore Methods

    public async Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string roleSql = "SELECT [Id] FROM [AspNetRoles] WHERE [NormalizedName] = @NormalizedName;";
        var roleId = await connection.QuerySingleOrDefaultAsync<string>(roleSql, new { NormalizedName = roleName.ToUpper() });

        if (roleId != null)
        {
            const string insertSql = "INSERT INTO [AspNetUserRoles] ([UserId], [RoleId]) VALUES (@UserId, @RoleId);";
            await connection.ExecuteAsync(insertSql, new { UserId = user.Id, RoleId = roleId });
        }
    }

    public async Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string roleSql = "SELECT [Id] FROM [AspNetRoles] WHERE [NormalizedName] = @NormalizedName;";
        var roleId = await connection.QuerySingleOrDefaultAsync<string>(roleSql, new { NormalizedName = roleName.ToUpper() });

        if (roleId != null)
        {
            const string deleteSql = "DELETE FROM [AspNetUserRoles] WHERE [UserId] = @UserId AND [RoleId] = @RoleId;";
            await connection.ExecuteAsync(deleteSql, new { UserId = user.Id, RoleId = roleId });
        }
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = @"
                SELECT 
                    r.[Name] 
                FROM [AspNetRoles] r
                INNER JOIN [AspNetUserRoles] ur ON ur.[RoleId] = r.[Id]
                WHERE ur.[UserId] = @UserId;";
        var roles = await connection.QueryAsync<string>(sql, new { UserId = user.Id });
        return roles.ToList();
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string roleSql = "SELECT [Id] FROM [AspNetRoles] WHERE [NormalizedName] = @NormalizedName;";
        var roleId = await connection.QuerySingleOrDefaultAsync<string>(roleSql, new { NormalizedName = roleName.ToUpper() });

        if (string.IsNullOrEmpty(roleId)) return false;

        const string sql = "SELECT COUNT(1) FROM [AspNetUserRoles] WHERE [UserId] = @UserId AND [RoleId] = @RoleId;";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = user.Id, RoleId = roleId });
        return count > 0;
    }

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = @"
                SELECT 
                    u.* FROM [AspNetUsers] u
                INNER JOIN [AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
                INNER JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                WHERE r.[NormalizedName] = @NormalizedName;";
        var users = await connection.QueryAsync<ApplicationUser>(sql, new { NormalizedName = roleName.ToUpper() });
        return users.ToList();
    }

    #endregion

    #region IUserLoginStore Methods

    public async Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Connection;
        const string sql = @"
                INSERT INTO [AspNetUserLogins] 
                (
                    [LoginProvider], 
                    [ProviderKey], 
                    [ProviderDisplayName], 
                    [UserId]
                )
                VALUES 
                (
                    @LoginProvider, 
                    @ProviderKey, 
                    @ProviderDisplayName, 
                    @UserId
                );";
        await connection.ExecuteAsync(sql, new
        {
            login.LoginProvider,
            login.ProviderKey,
            login.ProviderDisplayName,
            UserId = user.Id
        });
    }

    public async Task RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM [AspNetUserLogins] WHERE [LoginProvider] = @LoginProvider AND [ProviderKey] = @ProviderKey AND [UserId] = @UserId;";

        cancellationToken.ThrowIfCancellationRequested();

        using var connection = Connection;
        await connection.ExecuteAsync(sql, new { LoginProvider = loginProvider, ProviderKey = providerKey, UserId = user.Id });
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM [AspNetUserLogins] WHERE [UserId] = @UserId;";

        cancellationToken.ThrowIfCancellationRequested();

        using var connection = Connection;
        var logins = await connection.QueryAsync<UserLoginInfo>(sql, new { UserId = user.Id });

        return logins.ToList();
    }

    public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT u.* FROM [AspNetUsers] u
                INNER JOIN [AspNetUserLogins] l ON l.[UserId] = u.[Id]
                WHERE l.[LoginProvider] = @LoginProvider AND l.[ProviderKey] = @ProviderKey;";

        cancellationToken.ThrowIfCancellationRequested();

        using var connection = Connection;
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(sql, new { LoginProvider = loginProvider, ProviderKey = providerKey });
    }

    #endregion
}