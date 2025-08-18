using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Contracts.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace PredictionLeague.Web.Client.Authentication;

public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<ApiAuthenticationStateProvider> _logger;
    private readonly NavigationManager _navigationManager;
    private AuthenticationState? _cachedAuthenticationState;
    private bool _refreshAttempted;

    public ApiAuthenticationStateProvider(HttpClient httpClient, ILocalStorageService localStorage, ILogger<ApiAuthenticationStateProvider> logger, NavigationManager navigationManager)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _logger = logger;
        _navigationManager = navigationManager;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return _cachedAuthenticationState ??= await CreateAuthenticationStateAsync();
    }

    private async Task<AuthenticationState> CreateAuthenticationStateAsync()
    {
        const string loginCallbackPath = "/authentication/external-login-callback";
      
        if (_navigationManager.Uri.Contains(loginCallbackPath))
        {
            _logger.LogInformation("On login callback page. Skipping automatic refresh.");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        
        try
        {
            var accessToken = await _localStorage.GetItemAsync<string>("accessToken");

            if (!string.IsNullOrEmpty(accessToken))
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(accessToken);

                if (jwtToken.ValidTo > DateTime.UtcNow)
                {
                    _logger.LogInformation("Access token found and is valid.");
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                    return new AuthenticationState(CreateClaimsPrincipalFromToken(accessToken));
                }
                _logger.LogInformation("Access token is expired. Attempting to refresh.");
            }
           
            if (_refreshAttempted)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            
            var newAccessToken = await RefreshAccessTokenAsync();
            if (!string.IsNullOrEmpty(newAccessToken))
            {
                _logger.LogInformation("Token successfully refreshed.");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", newAccessToken);
                return new AuthenticationState(CreateClaimsPrincipalFromToken(newAccessToken));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during authentication state creation. Logging user out.");
        }

        await _localStorage.RemoveItemAsync("accessToken");
        _logger.LogInformation("Could not validate or refresh token. User is not authenticated.");
        await MarkUserAsLoggedOutAsync();
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
    public async Task<bool> LoginWithRefreshToken(string refreshToken)
    {
        _logger.LogInformation("Attempting to log in with refresh token from URL.");

        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("Refresh token from URL is null or empty.");
            return false;
        }

        var tokenModel = new RefreshTokenRequest { Token = refreshToken.Replace(' ', '+') };
        _logger.LogInformation("Sending refresh token to API: {Token}", tokenModel.Token);

        var response = await _httpClient.PostAsJsonAsync("api/auth/refresh-token", tokenModel);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("API call to refresh token failed with status code: {StatusCode}", response.StatusCode);
            return false;
        }
        _logger.LogInformation("API call to refresh token was successful.");

        var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
        if (authResponse == null)
        {
            _logger.LogError("Failed to deserialize successful authentication response.");
            return false;
        }

        _logger.LogInformation("Successfully deserialized authentication response. Storing access token.");
        await _localStorage.SetItemAsync("accessToken", authResponse.AccessToken);

        _logger.LogInformation("Notifying authentication state changed.");
        NotifyUserAuthentication();

        return true;
    }

    public async Task MarkUserAsAuthenticatedAsync(string accessToken)
    {
        await _localStorage.SetItemAsync("accessToken", accessToken);
        _refreshAttempted = false;
        NotifyUserAuthentication();
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await _localStorage.RemoveItemAsync("accessToken");
        _httpClient.DefaultRequestHeaders.Authorization = null;
        NotifyUserAuthentication();
    }

    private void NotifyUserAuthentication()
    {
        _cachedAuthenticationState = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<string?> RefreshAccessTokenAsync()
    {
        _refreshAttempted = true;
      
        try
        {
            var emptyContent = new StringContent("", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/auth/refresh-token", emptyContent);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
                if (authResponse?.AccessToken is not null)
                {
                    await _localStorage.SetItemAsync("accessToken", authResponse.AccessToken);
                    return authResponse.AccessToken;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while refreshing the access token.");
            return null;
        }
    }

    private static ClaimsPrincipal CreateClaimsPrincipalFromToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return new ClaimsPrincipal(new ClaimsIdentity());

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwtToken.Claims, "jwt", "FullName", "role");

        return new ClaimsPrincipal(identity);
    }
}