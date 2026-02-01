# Task 7: Reset Password Page

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a Blazor page where users can enter their new password after clicking the reset link in their email. The page validates the token, shows the appropriate form or error state, and auto-logs the user in on success.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web.Client/Components/Pages/Authentication/ResetPassword.razor` | Create | Reset password page |
| `PredictionLeague.Web.Client/Services/IAuthenticationService.cs` | Modify | Add reset password method |
| `PredictionLeague.Web.Client/Services/AuthenticationService.cs` | Modify | Implement reset password method |
| `PredictionLeague.Contracts/Authentication/ResetPasswordRequest.cs` | Already created | Used by service |

## Implementation Steps

### Step 1: Add Method to IAuthenticationService

```csharp
// In IAuthenticationService.cs
Task<ResetPasswordResponse> ResetPasswordAsync(string email, string token, string newPassword, string confirmPassword);
```

### Step 2: Implement in AuthenticationService

```csharp
// In AuthenticationService.cs
public async Task<ResetPasswordResponse> ResetPasswordAsync(string email, string token, string newPassword, string confirmPassword)
{
    var request = new ResetPasswordRequest
    {
        Email = email,
        Token = token,
        NewPassword = newPassword,
        ConfirmPassword = confirmPassword
    };

    var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", request);

    if (response.IsSuccessStatusCode)
    {
        var result = await response.Content.ReadFromJsonAsync<SuccessfulResetPasswordResponse>();
        if (result != null)
        {
            // Store the tokens (same pattern as login)
            await StoreTokensAsync(result.AccessToken, result.RefreshTokenForCookie);
            return result;
        }
    }

    var error = await response.Content.ReadFromJsonAsync<FailedResetPasswordResponse>();
    return error ?? new FailedResetPasswordResponse("An error occurred. Please try again.");
}
```

### Step 3: Create the Reset Password Page

```razor
@* ResetPassword.razor *@

@page "/authentication/reset-password"

@inject IAuthenticationService AuthenticationService
@inject NavigationManager NavigationManager

@using Microsoft.AspNetCore.WebUtilities
@using PredictionLeague.Contracts.Authentication

<PageTitle>Reset Password</PageTitle>

<div class="page-container">
    <div class="form-container shadow">
        <div class="position-relative mb-4">
            <div class="text-center mb-4 mt-2">
                <img src="/images/lion-outline-logo.jpg" alt="Lion Logo" class="max-w-60"/>
            </div>

            @if (_isTokenInvalid)
            {
                @* Invalid/Expired Token State *@
                <h3 class="text-center fw-bold text-white">Link Expired</h3>
                <p class="text-center text-white mb-4">
                    This password reset link has expired or is invalid. Please request a new one.
                </p>

                <div class="d-grid mt-4">
                    <NavLink href="/authentication/forgot-password" class="btn green-button text-purple-1000 input-height text-center">
                        Request New Link
                    </NavLink>
                </div>
            }
            else if (_isMissingParams)
            {
                @* Missing Parameters State *@
                <h3 class="text-center fw-bold text-white">Invalid Link</h3>
                <p class="text-center text-white mb-4">
                    This link appears to be incomplete. Please check your email and try clicking the link again.
                </p>

                <div class="d-grid mt-4">
                    <NavLink href="/authentication/forgot-password" class="btn green-button text-purple-1000 input-height text-center">
                        Request New Link
                    </NavLink>
                </div>
            }
            else
            {
                @* Normal Form State *@
                <h3 class="text-center fw-bold text-white">Reset Your Password</h3>
                <h5 class="text-center fw-normal text-white mb-4">Enter your new password below</h5>
            }
        </div>

        @if (!_isTokenInvalid && !_isMissingParams)
        {
            <EditForm Model="_model" OnValidSubmit="HandleSubmitAsync" FormName="resetPasswordForm">
                <DataAnnotationsValidator />
                <ApiError Message="@_errorMessage" />

                <div class="mb-3">
                    <div class="input-group">
                        <InputText id="newPassword"
                                   type="@(_showPassword ? "text" : "password")"
                                   class="form-control"
                                   @bind-Value="_model.NewPassword"
                                   data-lpignore="true"
                                   autocomplete="new-password"
                                   placeholder="New password"/>
                        <button type="button" class="btn password-toggle-btn" @onclick="TogglePasswordVisibility">
                            <i class="bi @(_showPassword ? "bi-eye-slash-fill" : "bi-eye-fill")"></i>
                        </button>
                    </div>
                    <ValidationMessage For="@(() => _model.NewPassword)" class="text-danger small mt-1" />
                </div>

                <div class="mb-3">
                    <div class="input-group">
                        <InputText id="confirmPassword"
                                   type="@(_showConfirmPassword ? "text" : "password")"
                                   class="form-control"
                                   @bind-Value="_model.ConfirmPassword"
                                   data-lpignore="true"
                                   autocomplete="new-password"
                                   placeholder="Confirm password"/>
                        <button type="button" class="btn password-toggle-btn" @onclick="ToggleConfirmPasswordVisibility">
                            <i class="bi @(_showConfirmPassword ? "bi-eye-slash-fill" : "bi-eye-fill")"></i>
                        </button>
                    </div>
                    <ValidationMessage For="@(() => _model.ConfirmPassword)" class="text-danger small mt-1" />
                </div>

                <p class="text-white-50 small mb-3">
                    Password must be at least 8 characters with uppercase, lowercase, and a number.
                </p>

                <div class="d-grid mt-4">
                    <button type="submit" class="btn green-button text-purple-1000 input-height" disabled="@_isBusy">
                        @if (_isBusy)
                        {
                            <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                            <span> Resetting...</span>
                        }
                        else
                        {
                            <span>Reset Password</span>
                        }
                    </button>
                </div>
            </EditForm>
        }
    </div>
</div>

@code {
    private readonly ResetPasswordModel _model = new();
    private string? _errorMessage;
    private bool _isBusy;
    private bool _showPassword;
    private bool _showConfirmPassword;
    private bool _isTokenInvalid;
    private bool _isMissingParams;

    private string? _email;
    private string? _token;

    protected override void OnInitialized()
    {
        var uri = new Uri(NavigationManager.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("email", out var email))
            _email = email;

        if (query.TryGetValue("token", out var token))
            _token = token;

        // Check if required parameters are present
        if (string.IsNullOrWhiteSpace(_email) || string.IsNullOrWhiteSpace(_token))
        {
            _isMissingParams = true;
        }
    }

    private void TogglePasswordVisibility()
    {
        _showPassword = !_showPassword;
    }

    private void ToggleConfirmPasswordVisibility()
    {
        _showConfirmPassword = !_showConfirmPassword;
    }

    private async Task HandleSubmitAsync()
    {
        _isBusy = true;
        _errorMessage = null;

        var result = await AuthenticationService.ResetPasswordAsync(
            _email!,
            _token!,
            _model.NewPassword,
            _model.ConfirmPassword
        );

        switch (result)
        {
            case SuccessfulResetPasswordResponse:
                // Auto-login successful, redirect to dashboard
                NavigationManager.NavigateTo("/", forceLoad: true);
                break;

            case FailedResetPasswordResponse failure:
                // Check if it's a token error
                if (failure.Message.Contains("invalid") || failure.Message.Contains("expired"))
                {
                    _isTokenInvalid = true;
                }
                else
                {
                    _errorMessage = failure.Message;
                }
                break;

            default:
                _errorMessage = "An unknown error occurred.";
                break;
        }

        _isBusy = false;
    }

    private class ResetPasswordModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Password is required")]
        [System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [System.ComponentModel.DataAnnotations.RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, and a number")]
        public string NewPassword { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Please confirm your password")]
        [System.ComponentModel.DataAnnotations.Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
```

## Code Patterns to Follow

### Query Parameter Parsing

Use `QueryHelpers.ParseQuery` from `Microsoft.AspNetCore.WebUtilities`:

```csharp
var uri = new Uri(NavigationManager.Uri);
var query = QueryHelpers.ParseQuery(uri.Query);

if (query.TryGetValue("paramName", out var value))
    _paramValue = value;
```

### Multiple UI States

Handle different states with boolean flags:

```csharp
private bool _isTokenInvalid;    // Token expired/invalid
private bool _isMissingParams;   // URL missing required params
// Default state: show form
```

### Password Visibility Toggle

Match the pattern from `Login.razor`:

```csharp
private bool _showPassword;
private bool _showConfirmPassword;

private void TogglePasswordVisibility() => _showPassword = !_showPassword;
private void ToggleConfirmPasswordVisibility() => _showConfirmPassword = !_showConfirmPassword;
```

### Auto-Login Redirect

After successful password reset, use `forceLoad: true` to ensure token state is refreshed:

```csharp
NavigationManager.NavigateTo("/", forceLoad: true);
```

## Verification

- [ ] Page accessible at `/authentication/reset-password?token=xxx&email=xxx`
- [ ] Missing parameters shows "Invalid Link" state
- [ ] Invalid/expired token shows "Link Expired" state
- [ ] Password visibility toggles work for both fields
- [ ] Validation enforces password requirements
- [ ] Validation ensures passwords match
- [ ] Submit button shows loading spinner
- [ ] Successful reset auto-logs in and redirects to dashboard
- [ ] Error messages display correctly
- [ ] "Request New Link" buttons navigate to forgot password page
- [ ] Page styling matches other auth pages
- [ ] Works on mobile devices

## Edge Cases to Consider

- **Missing email parameter** → Show "Invalid Link" state
- **Missing token parameter** → Show "Invalid Link" state
- **Expired token** → API returns error, show "Link Expired" state
- **Already-used token** → API returns error, show "Link Expired" state
- **Password too weak** → Validation error shown
- **Passwords don't match** → Validation error shown
- **Network error** → Generic error message
- **Token contains special characters** → URL encoding handles this

## Notes

- The page reads `token` and `email` from query parameters (set by the email link)
- Token is URL-decoded automatically by the query parser
- Using `forceLoad: true` on redirect ensures the authentication state is fully refreshed
- Password hint text reminds users of requirements before they type
- Two separate password visibility toggles for better UX
- The `Compare` attribute handles password matching validation
- Regex validation provides client-side feedback before hitting the API
