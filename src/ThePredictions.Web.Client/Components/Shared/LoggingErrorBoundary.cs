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

    protected override Task OnErrorAsync(Exception exception)
    {
        // A session that ended mid-request shouldn't surface as the generic error
        // UI. Navigate to login (the message is carried in SessionState and shown
        // by the login page) and recover so the error UI is cleared. Navigating
        // first means recovery re-renders the login route rather than the page
        // that just failed.
        if (exception is SessionExpiredException)
        {
            Navigation.NavigateTo("/authentication/login");
            Recover();
            return Task.CompletedTask;
        }

        Logger.LogError(exception, "Unhandled error caught by ErrorBoundary: {ErrorMessage}", exception.Message);
        return Task.CompletedTask;
    }
}
