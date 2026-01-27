using Microsoft.AspNetCore.Components.Authorization;
using PredictionLeague.Contracts.Authentication;
using System.Net.Http.Json;
using System.Text.Json;

namespace PredictionLeague.Web.Client.Authentication;

public class AuthenticationService(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider) : IAuthenticationService
{
    public async Task<AuthenticationResponse> RegisterAsync(RegisterRequest registerRequest)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        if (response.IsSuccessStatusCode)
        {
            var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (successResponse == null) 
                return new FailedAuthenticationResponse("Failed to process server response.");
          
            await ((ApiAuthenticationStateProvider)authenticationStateProvider).MarkUserAsAuthenticatedAsync(successResponse.AccessToken);
            return successResponse;
        }
       
        var errorContent = await response.Content.ReadAsStringAsync();

        try
        {
            var identityErrorResponse = JsonSerializer.Deserialize<IdentityErrorResponse>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (identityErrorResponse?.Errors.Any() == true)
                return new FailedAuthenticationResponse(string.Join("\n", identityErrorResponse.Errors));
        }
        catch (JsonException)
        {
        }

        try
        {
            var failureResponse = JsonSerializer.Deserialize<FailedAuthenticationResponse>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (failureResponse != null && !string.IsNullOrEmpty(failureResponse.Message))
                return failureResponse;
        }
        catch (JsonException)
        {
        }

        return new FailedAuthenticationResponse("An unexpected error occurred during registration.");
    }
    
    public async Task<AuthenticationResponse> LoginAsync(LoginRequest loginRequest)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
        if (response.IsSuccessStatusCode)
        {
            var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (successResponse == null) 
                return new FailedAuthenticationResponse("Failed to process server response.");
         
            await ((ApiAuthenticationStateProvider)authenticationStateProvider).MarkUserAsAuthenticatedAsync(successResponse.AccessToken);
            return successResponse;
        }

        var failureResponse = await response.Content.ReadFromJsonAsync<FailedAuthenticationResponse>();
        return failureResponse ?? new FailedAuthenticationResponse("Failed to process server response.");
    }
    
    public async Task LogoutAsync()
    {
        await httpClient.PostAsync("api/auth/logout", null);
        await ((ApiAuthenticationStateProvider)authenticationStateProvider).MarkUserAsLoggedOutAsync();
    }

    private class IdentityErrorResponse
    {
        public List<string> Errors { get; init; } = [];
    }
}