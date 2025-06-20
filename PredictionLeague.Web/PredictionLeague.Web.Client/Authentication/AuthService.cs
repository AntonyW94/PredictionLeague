using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Web.Client.Contracts.Auth;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PredictionLeague.Web.Client.Authentication;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ILocalStorageService _localStorage;

    public AuthService(HttpClient httpClient,
        AuthenticationStateProvider authenticationStateProvider,
        ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _authenticationStateProvider = authenticationStateProvider;
        _localStorage = localStorage;
    }

    public async Task<RegisterResponse> Register(RegisterRequest registerRequest)
    {
        var result = await _httpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        var content = await result.Content.ReadFromJsonAsync<RegisterResponse>();
        return content;
    }

    public async Task<AuthResponse> Login(LoginRequest loginRequest)
    {
        var result = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
        var content = await result.Content.ReadFromJsonAsync<AuthResponse>();

        if (result.IsSuccessStatusCode && !string.IsNullOrEmpty(content.Token))
        {
            await _localStorage.SetItemAsync("authToken", content.Token);
            ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsAuthenticated(content.Token);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", content.Token);
        }

        return content;
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("authToken");
        ((ApiAuthenticationStateProvider)_authenticationStateProvider).MarkUserAsLoggedOut();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}