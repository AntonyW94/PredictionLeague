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

public class ApiAuthenticationStateProvider(HttpClient httpClient, ILocalStorageService localStorage, ILogger<ApiAuthenticationStateProvider> logger, NavigationManager navigationManager) : AuthenticationStateProvider
{
    private AuthenticationState? _cachedAuthenticationState;
    private bool _refreshAttempted;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return _cachedAuthenticationState ??= await CreateAuthenticationStateAsync();
    }

    private async Task<AuthenticationState> CreateAuthenticationStateAsync()
    {
        const string loginCallbackPath = "/authentication/external-login-callback";
      
        if (navigationManager.Uri.Contains(loginCallbackPath))
        {
            logger.LogInformation("On login callback page. Skipping automatic refresh.");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        
        try
        {
            var accessToken = await localStorage.GetItemAsync<string>("accessToken");

            if (!string.IsNullOrEmpty(accessToken))
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(accessToken);

                if (jwtToken.ValidTo > DateTime.UtcNow)
                {
                    logger.LogInformation("Access token found and is valid.");
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                    return new AuthenticationState(CreateClaimsPrincipalFromToken(accessToken));
                }
                logger.LogInformation("Access token is expired. Attempting to refresh.");
            }
           
            if (_refreshAttempted)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            
            var newAccessToken = await RefreshAccessTokenAsync();
            if (!string.IsNullOrEmpty(newAccessToken))
            {
                logger.LogInformation("Token successfully refreshed.");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", newAccessToken);
                return new AuthenticationState(CreateClaimsPrincipalFromToken(newAccessToken));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during authentication state creation. Logging user out.");
        }

        await localStorage.RemoveItemAsync("accessToken");
        logger.LogInformation("Could not validate or refresh token. User is not authenticated.");
        await MarkUserAsLoggedOutAsync();
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
    public async Task<bool> LoginWithRefreshToken(string refreshToken)
    {
        logger.LogInformation("Attempting to log in with refresh token from URL.");

        if (string.IsNullOrEmpty(refreshToken))
        {
            logger.LogWarning("Refresh token from URL is null or empty.");
            return false;
        }

        var tokenModel = new RefreshTokenRequest { Token = refreshToken.Replace(' ', '+') };
        logger.LogDebug("Sending refresh token request to API");

        var response = await httpClient.PostAsJsonAsync("api/auth/refresh-token", tokenModel);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("API call to refresh token failed with status code: {StatusCode}", response.StatusCode);
            return false;
        }
        logger.LogInformation("API call to refresh token was successful.");

        var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
        if (authResponse == null)
        {
            logger.LogError("Failed to deserialize successful authentication response.");
            return false;
        }

        logger.LogInformation("Successfully deserialized authentication response. Storing access token.");
        await localStorage.SetItemAsync("accessToken", authResponse.AccessToken);

        logger.LogInformation("Notifying authentication state changed.");
        NotifyUserAuthentication();

        return true;
    }

    public async Task MarkUserAsAuthenticatedAsync(string accessToken)
    {
        await localStorage.SetItemAsync("accessToken", accessToken);
        _refreshAttempted = false;
        NotifyUserAuthentication();
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await localStorage.RemoveItemAsync("accessToken");
        httpClient.DefaultRequestHeaders.Authorization = null;
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
            var response = await httpClient.PostAsync("api/auth/refresh-token", emptyContent);

            if (!response.IsSuccessStatusCode) 
                return null;
            
            var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (authResponse?.AccessToken is null) 
                return null;
            
            await localStorage.SetItemAsync("accessToken", authResponse.AccessToken);
            return authResponse.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while refreshing the access token.");
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