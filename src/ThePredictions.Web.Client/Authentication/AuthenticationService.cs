using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Authorization;
using ThePredictions.Contracts.Authentication;
using ThePredictions.Web.Client.Services.Theme;
using System.Net.Http.Json;
using System.Text.Json;

namespace ThePredictions.Web.Client.Authentication;

[ExcludeFromCodeCoverage(Justification = "HTTP message handler plumbing: no branching logic of its own.")]
public class AuthenticationService(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider, IThemeService themeService) : IAuthenticationService
{
    public async Task<AuthenticationResponse> RegisterAsync(RegisterRequest registerRequest)
    {
        var response = await httpClient.PostAsJsonAsync("api/authentication/register", registerRequest);
        if (response.IsSuccessStatusCode)
        {
            var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (successResponse == null)
                return new FailedAuthenticationResponse("Failed to process server response.");

            await ((ApiAuthenticationStateProvider)authenticationStateProvider).MarkUserAsAuthenticatedAsync(successResponse.AccessToken);
            await themeService.SyncOnLoginAsync();
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
        var response = await httpClient.PostAsJsonAsync("api/authentication/login", loginRequest);
        if (response.IsSuccessStatusCode)
        {
            var successResponse = await response.Content.ReadFromJsonAsync<SuccessfulAuthenticationResponse>();
            if (successResponse == null)
                return new FailedAuthenticationResponse("Failed to process server response.");

            await ((ApiAuthenticationStateProvider)authenticationStateProvider).MarkUserAsAuthenticatedAsync(successResponse.AccessToken);
            await themeService.SyncOnLoginAsync();
            return successResponse;
        }

        try
        {
            var failureResponse = await response.Content.ReadFromJsonAsync<FailedAuthenticationResponse>();
            if (failureResponse != null && !string.IsNullOrEmpty(failureResponse.Message))
                return failureResponse;
        }
        catch (JsonException)
        {
        }

        return new FailedAuthenticationResponse("An unexpected error occurred during login.");
    }
    
    public async Task LogoutAsync()
    {
        await httpClient.PostAsync("api/authentication/logout", null);
        await ((ApiAuthenticationStateProvider)authenticationStateProvider).MarkUserAsLoggedOutAsync();
    }

    public async Task<bool> RequestPasswordResetAsync(string email)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/authentication/forgot-password", new { email });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(string token, string newPassword, string confirmPassword)
    {
        var request = new ResetPasswordRequest
        {
            Token = token,
            NewPassword = newPassword,
            ConfirmPassword = confirmPassword
        };

        var response = await httpClient.PostAsJsonAsync("api/authentication/reset-password", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<SuccessfulResetPasswordResponse>();
            if (result != null)
            {
                await ((ApiAuthenticationStateProvider)authenticationStateProvider).MarkUserAsAuthenticatedAsync(result.AccessToken);
                await themeService.SyncOnLoginAsync();
                return result;
            }
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<FailedResetPasswordResponse>();
            if (error != null && !string.IsNullOrEmpty(error.Message))
                return error;
        }
        catch (JsonException)
        {
        }

        return new FailedResetPasswordResponse("An error occurred. Please try again.");
    }

    private class IdentityErrorResponse
    {
        public List<string> Errors { get; init; } = [];
    }
}
