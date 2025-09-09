using Microsoft.AspNetCore.Identity;
using PredictionLeague.Application.Common.Models;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Infrastructure.Services;

public class UserManagerService : IUserManager
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserManagerService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    #region Create

    public async Task<UserManagerResult> CreateAsync(ApplicationUser user)
    {
        var result = await _userManager.CreateAsync(user);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> CreateAsync(ApplicationUser user, string password)
    {
        var result = await _userManager.CreateAsync(user, password);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    #endregion

    #region Read

    public async Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }

    public async Task<ApplicationUser?> FindByLoginAsync(string provider, string providerKey)
    {
        return await _userManager.FindByLoginAsync(provider, providerKey);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        return await _userManager.GetRolesAsync(user);
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName)
    {
        return await _userManager.IsInRoleAsync(user, roleName);
    }

    #endregion

    #region Update

    public async Task<UserManagerResult> UpdateAsync(ApplicationUser user)
    {
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> AddLoginAsync(ApplicationUser user, string provider, string providerKey)
    {
        var result = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        var result = await _userManager.AddToRoleAsync(user, role);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    #endregion

    #region Delete

    public async Task<UserManagerResult> DeleteAsync(ApplicationUser user)
    {
        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<UserManagerResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        var result = await _userManager.RemoveFromRolesAsync(user, roles);
        return result.Succeeded ? UserManagerResult.Success() : UserManagerResult.Failure(result.Errors.Select(e => e.Description));
    }

    #endregion
}