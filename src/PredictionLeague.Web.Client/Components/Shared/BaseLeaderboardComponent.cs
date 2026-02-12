using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace PredictionLeague.Web.Client.Components.Shared;

public class BaseLeaderboardComponent : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = null!;

    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateTask;
        var user = authState.User;
        _currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    protected string GetUserHighlightClass(string? userId)
    {
        return !string.IsNullOrEmpty(_currentUserId) && userId == _currentUserId ? "current-user-highlight" : string.Empty;
    }
}