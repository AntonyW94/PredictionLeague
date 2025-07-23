using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Contracts.Authentication;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AuthenticationService(HttpClient httpClient,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _httpClient = httpClient;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<bool> JoinPublicLeagueAsync(int leagueId)
    {
        var result = await _httpClient.PostAsync($"api/leagues/{leagueId}/join", null);
        return result.IsSuccessStatusCode;
    }
   
    public async Task<AuthenticationResponse> RegisterAsync(RegisterRequest registerRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/authentication/register", registerRequest);
        if (response.IsSuccessStatusCode)
        {
            var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (successResponse == null) 
                return new FailedAuthenticationResponse("Failed to process server response.");
          
            ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsAuthenticated(successResponse);
            return successResponse;
        }

        var failureResponse = await response.Content.ReadFromJsonAsync<FailedAuthenticationResponse>();
        return failureResponse ?? new FailedAuthenticationResponse("Failed to process server response.");
    }
    
    public async Task<AuthenticationResponse> LoginAsync(LoginRequest loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/authentication/login", loginRequest);
        if (response.IsSuccessStatusCode)
        {
            var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (successResponse == null) 
                return new FailedAuthenticationResponse("Failed to process server response.");
         
            ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsAuthenticated(successResponse);
            return successResponse;
        }

        var failureResponse = await response.Content.ReadFromJsonAsync<FailedAuthenticationResponse>();
        return failureResponse ?? new FailedAuthenticationResponse("Failed to process server response.");
    }
    
    public async Task LogoutAsync()
    {
        await _httpClient.PostAsync("api/authentication/logout", null);

        ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsLoggedOut();
    }
}