using PredictionLeague.Application.Common.Models;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Services;

public interface IUserManager
{
    #region Create
      
    Task<UserManagerResult> CreateAsync(ApplicationUser user);
    Task<UserManagerResult> CreateAsync(ApplicationUser user, string password);

    #endregion

    #region Read

    Task<ApplicationUser?> FindByEmailAsync(string email);
    Task<ApplicationUser?> FindByIdAsync(string userId);
    Task<ApplicationUser?> FindByLoginAsync(string provider, string providerKey);
    Task<IList<string>> GetRolesAsync(ApplicationUser user);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);
    Task<bool> IsInRoleAsync(ApplicationUser user, string roleName);
    Task<bool> HasPasswordAsync(ApplicationUser user);

    #endregion

    #region Update

    Task<UserManagerResult> UpdateAsync(ApplicationUser user);
    Task<UserManagerResult> AddLoginAsync(ApplicationUser user, string provider, string providerKey);
    Task<UserManagerResult> AddToRoleAsync(ApplicationUser user, string role);
    Task<UserManagerResult> ResetPasswordDirectAsync(ApplicationUser user, string newPassword);

    #endregion

    #region Delete
      
    Task<UserManagerResult> DeleteAsync(ApplicationUser user);
    Task<UserManagerResult> RemoveFromRolesAsync(ApplicationUser user, IEnumerable<string> roles);

    #endregion
}