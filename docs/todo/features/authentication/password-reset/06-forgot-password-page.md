# Task 6: Forgot Password Page

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a Blazor page where users can enter their email to request a password reset link. After submission, show a success message regardless of whether the email exists (for security).

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web.Client/Components/Pages/Authentication/ForgotPassword.razor` | Create | Forgot password page |
| `PredictionLeague.Web.Client/Services/IAuthenticationService.cs` | Modify | Add forgot password method |
| `PredictionLeague.Web.Client/Services/AuthenticationService.cs` | Modify | Implement forgot password method |
| `PredictionLeague.Web.Client/Components/Pages/Authentication/Login.razor` | Modify | Add "Forgot password?" link |

## Implementation Steps

### Step 1: Add Method to IAuthenticationService

```csharp
// In IAuthenticationService.cs
Task<bool> RequestPasswordResetAsync(string email);
```

### Step 2: Implement in AuthenticationService

```csharp
// In AuthenticationService.cs
public async Task<bool> RequestPasswordResetAsync(string email)
{
    try
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password", new { email });
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}
```

### Step 3: Create the Forgot Password Page

```razor
@* ForgotPassword.razor *@

@page "/authentication/forgot-password"

@inject IAuthenticationService AuthenticationService
@inject NavigationManager NavigationManager

<PageTitle>Forgot Password</PageTitle>

<div class="page-container">
    <div class="form-container shadow">
        <div class="position-relative mb-4">
            <NavLink href="/authentication/login" class="back-button flex-center" title="Back to Login">
                <span class="bi bi-arrow-left-short"></span> Back to Login
            </NavLink>

            <div class="text-center mb-4 mt-2">
                <img src="/images/lion-outline-logo.jpg" alt="Lion Logo" class="max-w-60"/>
            </div>

            @if (_isSubmitted)
            {
                <h3 class="text-center fw-bold text-white">Check Your Email</h3>
                <p class="text-center text-white mb-4">
                    If an account exists with that email, you'll receive a password reset link shortly.
                </p>

                <div class="d-grid mt-4">
                    <NavLink href="/authentication/login" class="btn green-button text-purple-1000 input-height text-center">
                        Back to Login
                    </NavLink>
                </div>
            }
            else
            {
                <h3 class="text-center fw-bold text-white">Forgot Password</h3>
                <h5 class="text-center fw-normal text-white mb-4">Enter your email to receive a reset link</h5>
            }
        </div>

        @if (!_isSubmitted)
        {
            <EditForm Model="_model" OnValidSubmit="HandleSubmitAsync" FormName="forgotPasswordForm">
                <DataAnnotationsValidator />
                <ApiError Message="@_errorMessage" />

                <div class="mb-3">
                    <InputText id="email"
                               class="form-control"
                               @bind-Value="_model.Email"
                               data-lpignore="true"
                               autocomplete="off"
                               placeholder="Email address"/>
                    <ValidationMessage For="@(() => _model.Email)" class="text-danger small mt-1" />
                </div>

                <div class="d-grid mt-4">
                    <button type="submit" class="btn green-button text-purple-1000 input-height" disabled="@_isBusy">
                        @if (_isBusy)
                        {
                            <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                            <span> Sending...</span>
                        }
                        else
                        {
                            <span>Send Reset Link</span>
                        }
                    </button>
                </div>
            </EditForm>
        }
    </div>
</div>

@code {
    private readonly ForgotPasswordModel _model = new();
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isSubmitted;

    private async Task HandleSubmitAsync()
    {
        _isBusy = true;
        _errorMessage = null;

        var success = await AuthenticationService.RequestPasswordResetAsync(_model.Email);

        if (success)
        {
            _isSubmitted = true;
        }
        else
        {
            _errorMessage = "Something went wrong. Please try again.";
        }

        _isBusy = false;
    }

    private class ForgotPasswordModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;
    }
}
```

### Step 4: Add "Forgot Password?" Link to Login Page

In `Login.razor`, add a link below the password field:

```razor
@* Find this section in Login.razor *@
<div class="mb-3">
    <div class="input-group">
        <InputText id="password"
                   type="@(_showPassword ? "text" : "password")"
                   ...
    </div>
    <StyledValidationMessage For="@(() => _model.Password)" />
</div>

@* Add this after the password field *@
<div class="text-end mb-3">
    <NavLink href="/authentication/forgot-password" class="text-white small text-decoration-none">
        Forgot password?
    </NavLink>
</div>
```

## Code Patterns to Follow

### Page Structure

Follow the existing `Login.razor` and `Register.razor` structure:
- `page-container` wrapper
- `form-container shadow` for the card
- `back-button` for navigation
- Lion logo centred at top
- `EditForm` with validation

### State Management

Use simple boolean flags for UI state:

```csharp
private bool _isBusy;        // Shows loading spinner
private bool _isSubmitted;   // Shows success message
private string? _errorMessage;
```

### Form Submission Pattern

```csharp
private async Task HandleSubmitAsync()
{
    _isBusy = true;
    _errorMessage = null;

    // ... do work ...

    _isBusy = false;
}
```

## Verification

- [ ] Page accessible at `/authentication/forgot-password`
- [ ] Page styling matches Login and Register pages
- [ ] "Back to Login" link works
- [ ] Email validation shows error for invalid format
- [ ] Submit button shows loading spinner while processing
- [ ] Success message appears after submission
- [ ] "Back to Login" button appears after submission
- [ ] "Forgot password?" link added to Login page
- [ ] Works on mobile devices

## Edge Cases to Consider

- **Empty email** → Validation message shown
- **Invalid email format** → Validation message shown
- **API error** → Show generic error message
- **Network timeout** → Show generic error message
- **Rate limited** → API returns error, show generic message

## Notes

- The success message is intentionally vague ("If an account exists...") for security
- Using `DataAnnotationsValidator` instead of `FluentValidationValidator` for simple client-side validation
- The `ForgotPasswordModel` is a private nested class since it's only used by this page
- The page doesn't redirect after success - shows message in place for better UX
- No need for password visibility toggle on this page (no password field)
