using System.Security.Claims;
using PredictionLeague.Application.Services;
using PredictionLeague.Domain.Common.Constants;

namespace PredictionLeague.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public bool IsAdministrator => _httpContextAccessor.HttpContext?.User.IsInRole(RoleNames.Administrator) ?? false;

    public void EnsureAdministrator()
    {
        if (!IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required to access this resource.");

        if (!IsAdministrator)
            throw new UnauthorizedAccessException("Administrator privileges are required to access this resource.");
    }
}
