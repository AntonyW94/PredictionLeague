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

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest registerRequest)
    {
        var result = await _httpClient.PostAsJsonAsync("api/authentication/register", registerRequest);
        return await result.Content.ReadFromJsonAsync<RegisterResponse>() ?? new RegisterResponse { IsSuccess = false, Message = "Failed to process server response." };
    }
  
    public async Task<AuthenticationResponse> LoginAsync(LoginRequest loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/authentication/login", loginRequest);
        if (response.IsSuccessStatusCode)
        {
            var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (successResponse != null)
            {
                ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsAuthenticated(successResponse);
                return successResponse;
            }
        }
        else
        {
            var failureResponse = await response.Content.ReadFromJsonAsync<FailedAuthenticationResponse>();
            if (failureResponse != null)
                return failureResponse;
        }

        return new FailedAuthenticationResponse("Failed to process server response.");
    }
    
    //public async Task<AuthenticationResponse> LoginAsync(LoginRequest loginRequest)
    //{
    //    var result = await _httpClient.PostAsJsonAsync("api/authentication/login", loginRequest);
    //    var authenticationResponse = await result.Content.ReadFromJsonAsync<AuthenticationResponse>();
    //    if (authenticationResponse != null && authenticationResponse.IsSuccess)
    //        await ((ApiAuthenticationStateProvider)_authenticationStateProvider).Login(authenticationResponse);
        
    //    return authenticationResponse ?? new FailedAuthenticationResponse("Failed to process server response.");
    //}

    public async Task LogoutAsync()
    {
        await ((ApiAuthenticationStateProvider)_authenticationStateProvider).Logout();
    }
}