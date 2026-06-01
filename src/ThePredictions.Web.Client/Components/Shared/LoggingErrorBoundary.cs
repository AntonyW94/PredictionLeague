using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using ThePredictions.Web.Client.Authentication;

namespace ThePredictions.Web.Client.Components.Shared;

public class LoggingErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private SessionState SessionState { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        // A session that ended mid-request shouldn't surface as the generic error
        // UI - clear the error and send the user to login with a friendly message.
        if (exception is SessionExpiredException)
        {
            var message = SessionState.LogoutMessage;
            SessionState.LogoutMessage = null;

            Recover();

            var loginUrl = "/authentication/login";
            if (!string.IsNullOrEmpty(message))
                loginUrl += $"?error={Uri.EscapeDataString(message)}";

            Navigation.NavigateTo(loginUrl);
            return Task.CompletedTask;
        }

        Logger.LogError(exception, "Unhandled error caught by ErrorBoundary: {ErrorMessage}", exception.Message);
        return Task.CompletedTask;
    }
}
