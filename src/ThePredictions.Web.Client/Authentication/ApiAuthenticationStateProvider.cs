using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ThePredictions.Contracts.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
            var (accessToken, status) = await GetValidAccessTokenAsync();
            if (!string.IsNullOrEmpty(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                return new AuthenticationState(CreateClaimsPrincipalFromToken(accessToken));
            }

            if (status == TokenRefreshStatus.TransientFailure)
            {
                // The refresh endpoint was unreachable (e.g. the API restarting during
                // a deploy). The refresh token may still be valid, so leave it in place
                // and stay anonymous for now - a later attempt/reload re-authenticates.
                logger.LogInformation("Transient refresh failure during auth-state creation; leaving the session intact.");
                return Anonymous();
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
    public async Task<(string? Token, TokenRefreshStatus Status)> GetValidAccessTokenAsync(bool forceRefresh = false)
    {
        var accessToken = await localStorage.GetItemAsync<string>(AccessTokenKey);

        if (!forceRefresh && !string.IsNullOrEmpty(accessToken) && IsTokenValid(accessToken))
            return (accessToken, TokenRefreshStatus.Succeeded);

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

    private async Task<(string? Token, TokenRefreshStatus Status)> RefreshAccessTokenAsync(string? knownStaleToken)
    {
        await _refreshLock.WaitAsync();

        try
        {
            // A concurrent caller may have refreshed while we were waiting on the
            // lock; if the stored token has changed and is valid, reuse it rather
            // than rotating the refresh token again.
            var current = await localStorage.GetItemAsync<string>(AccessTokenKey);
            if (!string.IsNullOrEmpty(current) && current != knownStaleToken && IsTokenValid(current))
                return (current, TokenRefreshStatus.Succeeded);

            HttpResponseMessage response;
            try
            {
                var emptyContent = new StringContent("", Encoding.UTF8, "application/json");
                response = await httpClient.PostAsync("api/authentication/refresh-token", emptyContent);
            }
            catch (Exception ex)
            {
                // Network error / API unreachable (e.g. mid-deploy). The refresh token
                // may still be valid, so treat this as transient and keep the session.
                logger.LogWarning(ex, "Transient error reaching the refresh endpoint; keeping the session.");
                return (null, TokenRefreshStatus.TransientFailure);
            }

            return await ReadRefreshResponseAsync(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error refreshing the access token; keeping the session.");
            return (null, TokenRefreshStatus.TransientFailure);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Stores and returns the new token on success. A 400 or 401 means the refresh token itself was
    /// rejected - expired, revoked or missing - so the session is genuinely over. Anything else
    /// (429, 5xx, timeout) is transient and must not log the user out.
    /// </summary>
    private async Task<(string? Token, TokenRefreshStatus Status)> ReadRefreshResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var authResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (authResponse?.AccessToken is null)
                return (null, TokenRefreshStatus.TransientFailure);

            await localStorage.SetItemAsync(AccessTokenKey, authResponse.AccessToken);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authResponse.AccessToken);
            return (authResponse.AccessToken, TokenRefreshStatus.Succeeded);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            return (null, TokenRefreshStatus.InvalidSession);

        logger.LogWarning("Transient {StatusCode} from the refresh endpoint; keeping the session.", response.StatusCode);
        return (null, TokenRefreshStatus.TransientFailure);
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

    /// <summary>
    /// The caller only reaches this with a non-empty token, so there is no empty case to guard.
    /// </summary>
    private static ClaimsPrincipal CreateClaimsPrincipalFromToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwtToken.Claims, "jwt", "FullName", "role");

        return new ClaimsPrincipal(identity);
    }
}
