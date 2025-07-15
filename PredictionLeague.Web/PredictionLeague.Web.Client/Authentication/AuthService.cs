using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Contracts.Auth;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Authentication;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AuthService(HttpClient httpClient,
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

    public async Task<RegisterResponse> Register(RegisterRequest registerRequest)
    {
        var result = await _httpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        return await result.Content.ReadFromJsonAsync<RegisterResponse>() ?? new RegisterResponse { IsSuccess = false, Message = "Failed to process server response." };
    }

    public async Task<AuthResponse> Login(LoginRequest loginRequest)
    {
        var result = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
        var authResponse = await result.Content.ReadFromJsonAsync<AuthResponse>();
        if (authResponse != null && authResponse.IsSuccess)
            await ((ApiAuthenticationStateProvider)_authenticationStateProvider).Login(authResponse);
        
        return authResponse ?? new AuthResponse { IsSuccess = false, Message = "Failed to process server response." };
    }

    public async Task Logout()
    {
        await ((ApiAuthenticationStateProvider)_authenticationStateProvider).Logout();
    }
}