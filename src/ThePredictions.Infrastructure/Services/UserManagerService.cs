using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Services;

[ExcludeFromCodeCoverage(Justification = "ASP.NET Identity wrapper: forwards to UserManager and maps its result, exercised end to end.")]
public class UserManagerService(UserManager<ApplicationUser> userManager) : IUserManager
{
    #region Create

    public async Task<UserManagerResult> CreateAsync(ApplicationUser user)
    {
        var result = await userManager.CreateAsync(user);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> CreateAsync(ApplicationUser user, string password)
    {
        var result = await userManager.CreateAsync(user, password);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    #endregion

    #region Read

    public async Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        return await userManager.FindByEmailAsync(email);
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId)
    {
        return await userManager.FindByIdAsync(userId);
    }

    public async Task<ApplicationUser?> FindByLoginAsync(string provider, string providerKey)
    {
        return await userManager.FindByLoginAsync(provider, providerKey);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        return await userManager.GetRolesAsync(user);
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        return await userManager.CheckPasswordAsync(user, password);
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName)
    {
        return await userManager.IsInRoleAsync(user, roleName);
    }

    public async Task<bool> HasPasswordAsync(ApplicationUser user)
    {
        return await userManager.HasPasswordAsync(user);
    }

    #endregion

    #region Update

    public async Task<UserManagerResult> UpdateAsync(ApplicationUser user)
    {
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> AddLoginAsync(ApplicationUser user, string provider, string providerKey)
    {
        var result = await userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        var result = await userManager.AddToRoleAsync(user, role);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> ResetPasswordDirectAsync(ApplicationUser user, string newPassword)
    {
        // Remove existing password (if any) and add new one
        var removeResult = await userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
            return UserManagerResult.Failure(removeResult.Errors.Select(e => e.Description));

        var addResult = await userManager.AddPasswordAsync(user, newPassword);
        return addResult.Succeeded
            ? UserManagerResult.Success()
            : UserManagerResult.Failure(addResult.Errors.Select(e => e.Description));
    }

    #endregion

    #region Delete

    public async Task<UserManagerResult> DeleteAsync(ApplicationUser user)
    {
        var result = await userManager.DeleteAsync(user);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        var result = await userManager.RemoveFromRolesAsync(user, roles);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    #endregion
}