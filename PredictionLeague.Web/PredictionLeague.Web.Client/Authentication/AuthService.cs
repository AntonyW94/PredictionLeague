using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Shared.Auth;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PredictionLeague.Web.Client.Authentication;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ILocalStorageService _localStorage;
    private readonly JsonSerializerOptions _options;

    public AuthService(HttpClient httpClient,
        AuthenticationStateProvider authenticationStateProvider,
        ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _authenticationStateProvider = authenticationStateProvider;
        _localStorage = localStorage;
        _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task<bool> JoinPublicLeagueAsync(int leagueId)
    {
        var result = await _httpClient.PostAsync($"api/leagues/{leagueId}/join", null);
        return result.IsSuccessStatusCode;
    }

    public async Task<RegisterResponse> Register(RegisterRequest registerRequest)
    {
        var result = await _httpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        var content = await result.Content.ReadFromJsonAsync<RegisterResponse>(_options);
        
        return content ?? new RegisterResponse { IsSuccess = false, Message = "Registration failed" };
    }

    public async Task<AuthResponse> Login(LoginRequest loginRequest)
    {
        var result = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
      
        var content = await result.Content.ReadFromJsonAsync<AuthResponse>(_options);
        if (content == null)
            return new AuthResponse { IsSuccess = false, Message = "Login failed" };

        if (!result.IsSuccessStatusCode || string.IsNullOrEmpty(content.Token)) 
            return content;
        
        await _localStorage.SetItemAsync("authToken", content.Token);
        ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsAuthenticated(content.Token);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", content.Token);

        return content; 
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("authToken");
        ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsLoggedOut();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}