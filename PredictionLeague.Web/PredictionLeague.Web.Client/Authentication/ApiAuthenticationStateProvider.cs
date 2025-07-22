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
        if (authenticationResponse != null && authenticationResponse.IsSuccess && !string.IsNullOrEmpty(authenticationResponse.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authenticationResponse.AccessToken);
            _currentUser = CreateClaimsPrincipalFromToken(authenticationResponse.AccessToken);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        
        return new AuthenticationState(_currentUser);
    }

    public void MarkUserAsAuthenticated(SuccessfulAuthenticationResponse authResponse)
    {
        var claimsPrincipal = CreateClaimsPrincipalFromToken(authResponse.AccessToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authResponse.AccessToken);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
    }
    
    public void MarkUserAsLoggedOut()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
    }
    
    //public Task Login(AuthenticationResponse? authenticationResponse)
    //{
    //    if (authenticationResponse != null && authenticationResponse.IsSuccess && !string.IsNullOrEmpty(authenticationResponse.AccessToken))
    //    {
    //        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authenticationResponse.AccessToken);
    //        _currentUser = CreateClaimsPrincipalFromToken(authenticationResponse.AccessToken);
    //    }
    //    else
    //    {
    //        _httpClient.DefaultRequestHeaders.Authorization = null;
    //        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
    //    }
        
    //    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

    //    return Task.CompletedTask;
    //}

    public async Task NotifyUserAuthentication()
    {
        var authenticationResponse = await RefreshAccessToken();
        if (authenticationResponse != null && authenticationResponse.IsSuccess && !string.IsNullOrEmpty(authenticationResponse.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", authenticationResponse.AccessToken);
            _currentUser = CreateClaimsPrincipalFromToken(authenticationResponse.AccessToken);
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
    private async Task<SuccessfulAuthenticationResponse?> RefreshAccessToken()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/authentication/refresh-token", null);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();

            return null;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
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