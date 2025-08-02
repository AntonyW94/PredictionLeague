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
    private Task<AuthenticationState>? _cachedAuthenticationStateTask;
    private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

    public ApiAuthenticationStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return _cachedAuthenticationStateTask ??= CreateAuthenticationStateAsync();
    }

    public async Task<AuthenticationState> CreateAuthenticationStateAsync()
    {
        var authenticationResponse = await RefreshAccessToken();
        if (authenticationResponse != null && authenticationResponse.IsSuccess && !string.IsNullOrEmpty(authenticationResponse.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authenticationResponse.AccessToken);
            var claimsPrincipal = CreateClaimsPrincipalFromToken(authenticationResponse.AccessToken);
            return new AuthenticationState(claimsPrincipal);
        }

        _httpClient.DefaultRequestHeaders.Authorization = null;
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public void MarkUserAsAuthenticated(SuccessfulAuthenticationResponse authResponse)
    {
        var claimsPrincipal = CreateClaimsPrincipalFromToken(authResponse.AccessToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authResponse.AccessToken);

        _cachedAuthenticationStateTask = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
    }
    
    public void MarkUserAsLoggedOut()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());

        _cachedAuthenticationStateTask = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
    }

    public void NotifyUserAuthentication()
    {
        _cachedAuthenticationStateTask = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<SuccessfulAuthenticationResponse?> RefreshAccessToken()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/auth/refresh-token", null);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();

            return null;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.BadRequest)
                return null;

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