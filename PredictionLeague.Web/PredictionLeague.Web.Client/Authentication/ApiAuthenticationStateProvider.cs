using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Contracts.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;

namespace PredictionLeague.Web.Client.Authentication;

public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<ApiAuthenticationStateProvider> _logger;
    private Task<AuthenticationState>? _cachedAuthenticationStateTask;

    public ApiAuthenticationStateProvider(HttpClient httpClient, ILocalStorageService localStorage, ILogger<ApiAuthenticationStateProvider> logger)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _logger = logger;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return _cachedAuthenticationStateTask ??= CreateAuthenticationStateAsync();
    }

    private async Task<AuthenticationState> CreateAuthenticationStateAsync()
    {
        var accessToken = await _localStorage.GetItemAsync<string>("accessToken");

        // If we have a token, check if it's still valid
        if (!string.IsNullOrEmpty(accessToken))
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(accessToken);
            if (jwtToken.ValidTo > DateTime.UtcNow)
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                return new AuthenticationState(CreateClaimsPrincipalFromToken(accessToken));
            }
        }

        // If we don't have a valid token (either it's missing or expired), we must try to refresh it using the HttpOnly cookie.
        // This handles both subsequent page loads and the initial load after an external login.
        var wasRefreshed = await RefreshAccessToken();
        if (wasRefreshed)
        {
            accessToken = await _localStorage.GetItemAsync<string>("accessToken");
            if (!string.IsNullOrEmpty(accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                return new AuthenticationState(CreateClaimsPrincipalFromToken(accessToken));
            }
        }

        // If we can't get a valid token, ensure the user is fully logged out.
        await _localStorage.RemoveItemAsync("accessToken");
        _httpClient.DefaultRequestHeaders.Authorization = null;
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

        var tokenModel = new { Token = refreshToken.Replace(' ', '+') };
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

    public async Task MarkUserAsAuthenticated(SuccessfulAuthenticationResponse authResponse)
    {
        await _localStorage.SetItemAsync("accessToken", authResponse.AccessToken);
        NotifyUserAuthentication();
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _localStorage.RemoveItemAsync("accessToken");
        NotifyUserAuthentication();
    }

    private void NotifyUserAuthentication()
    {
        _cachedAuthenticationStateTask = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<bool> RefreshAccessToken()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/auth/refresh-token", null);
            if (response.IsSuccessStatusCode)
            {
                var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
                if (successResponse != null)
                {
                    await _localStorage.SetItemAsync("accessToken", successResponse.AccessToken);
                    return true;
                }
            }

            return false;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.BadRequest)
                return false;

            throw;
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