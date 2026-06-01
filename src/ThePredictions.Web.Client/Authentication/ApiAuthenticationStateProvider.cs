using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ThePredictions.Contracts.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace ThePredictions.Web.Client.Authentication;

public class ApiAuthenticationStateProvider(HttpClient httpClient, ILocalStorageService localStorage, ILogger<ApiAuthenticationStateProvider> logger, NavigationManager navigationManager) : AuthenticationStateProvider
{
    private const string AccessTokenKey = "accessToken";

    // Refresh slightly before expiry so an in-flight request isn't sent with a
    // token that lapses between the validity check and the server receiving it.
    private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(30);

    // Serialises refresh attempts so a burst of components/requests hitting an
    // expired token triggers a single network refresh, not one per caller.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private AuthenticationState? _cachedAuthenticationState;

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
            return Anonymous();
        }

        try
        {
            var accessToken = await GetValidAccessTokenAsync();
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                return new AuthenticationState(CreateClaimsPrincipalFromToken(accessToken));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during authentication state creation. Logging user out.");
        }

        logger.LogInformation("Could not validate or refresh token. User is not authenticated.");

        // Clean up the stale token directly rather than calling
        // MarkUserAsLoggedOutAsync: we're already inside the auth-state
        // computation, and notifying here would re-enter GetAuthenticationStateAsync
        // and spin in a loop (page flicker + repeated refresh calls).
        await localStorage.RemoveItemAsync(AccessTokenKey);
        httpClient.DefaultRequestHeaders.Authorization = null;
        return Anonymous();
    }

    /// <summary>
    /// Returns a non-expired access token, refreshing via the refresh-token cookie
    /// if the stored token is missing or about to expire. Returns null when the
    /// user can no longer be authenticated.
    /// </summary>
    /// <param name="forceRefresh">
    /// When true the stored token is treated as stale even if it still looks valid
    /// (used after a 401, where the server has rejected an otherwise in-date token).
    /// </param>
    public async Task<string?> GetValidAccessTokenAsync(bool forceRefresh = false)
    {
        var accessToken = await localStorage.GetItemAsync<string>(AccessTokenKey);

        if (!forceRefresh && !string.IsNullOrEmpty(accessToken) && IsTokenValid(accessToken))
            return accessToken;

        return await RefreshAccessTokenAsync(accessToken);
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

        var response = await httpClient.PostAsJsonAsync("api/authentication/refresh-token", tokenModel);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("API call to refresh token failed with status code: {StatusCode}", response.StatusCode);
            return false;
        }
        logger.LogInformation("API call to refresh token was successful.");

        var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
        if (authResponse == null)
        {
            logger.LogError("Failed to deserialise successful authentication response.");
            return false;
        }

        logger.LogInformation("Successfully deserialised authentication response. Storing access token.");
        await localStorage.SetItemAsync(AccessTokenKey, authResponse.AccessToken);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authResponse.AccessToken);

        logger.LogInformation("Notifying authentication state changed.");
        NotifyUserAuthentication();

        return true;
    }

    public async Task MarkUserAsAuthenticatedAsync(string accessToken)
    {
        await localStorage.SetItemAsync(AccessTokenKey, accessToken);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
        NotifyUserAuthentication();
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await localStorage.RemoveItemAsync(AccessTokenKey);
        httpClient.DefaultRequestHeaders.Authorization = null;
        NotifyUserAuthentication();
    }

    private void NotifyUserAuthentication()
    {
        _cachedAuthenticationState = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<string?> RefreshAccessTokenAsync(string? knownStaleToken)
    {
        await _refreshLock.WaitAsync();

        try
        {
            // A concurrent caller may have refreshed while we were waiting on the
            // lock; if the stored token has changed and is valid, reuse it rather
            // than rotating the refresh token again.
            var current = await localStorage.GetItemAsync<string>(AccessTokenKey);
            if (!string.IsNullOrEmpty(current) && current != knownStaleToken && IsTokenValid(current))
                return current;

            var emptyContent = new StringContent("", Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("api/authentication/refresh-token", emptyContent);

            if (!response.IsSuccessStatusCode)
                return null;

            var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (authResponse?.AccessToken is null)
                return null;

            await localStorage.SetItemAsync(AccessTokenKey, authResponse.AccessToken);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authResponse.AccessToken);
            return authResponse.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while refreshing the access token.");
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool IsTokenValid(string token)
    {
        try
        {
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwtToken.ValidTo > DateTime.UtcNow.Add(ExpiryLeeway);
        }
        catch
        {
            return false;
        }
    }

    private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));

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
