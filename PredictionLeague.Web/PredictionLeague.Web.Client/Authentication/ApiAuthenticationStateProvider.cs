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
    private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

    public ApiAuthenticationStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

        var authenticationResponse = await RefreshAccessToken();
        if (authenticationResponse != null && authenticationResponse.IsSuccess && !string.IsNullOrEmpty(authenticationResponse.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authenticationResponse.Token);
            _currentUser = CreateClaimsPrincipalFromToken(authenticationResponse.Token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        
        return new AuthenticationState(_currentUser);
    }

    public Task Login(AuthenticationResponse? authenticationResponse)
    {
        if (authenticationResponse != null && authenticationResponse.IsSuccess && !string.IsNullOrEmpty(authenticationResponse.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authenticationResponse.Token);
            _currentUser = CreateClaimsPrincipalFromToken(authenticationResponse.Token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

        return Task.CompletedTask;
    }

    public async Task NotifyUserAuthentication()
    {
        var authenticationResponse = await RefreshAccessToken();
        if (authenticationResponse != null && authenticationResponse.IsSuccess && !string.IsNullOrEmpty(authenticationResponse.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authenticationResponse.Token);
            _currentUser = CreateClaimsPrincipalFromToken(authenticationResponse.Token);
        }
        
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public async Task Logout()
    {
        await _httpClient.PostAsync("api/authentication/logout", null);
        
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
       
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task<AuthenticationResponse?> RefreshAccessToken()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/authentication/refresh-token", null);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            return null;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
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
        var identity = new ClaimsIdentity(jwtToken.Claims,
            "jwt",
            "FullName",
            "role"); 
        
        return new ClaimsPrincipal(identity);
    }
}