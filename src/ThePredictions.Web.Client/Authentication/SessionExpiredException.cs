namespace ThePredictions.Web.Client.Authentication;

/// <summary>
/// Thrown by <see cref="AuthorizationMessageHandler"/> when an authenticated API
/// call fails with 401 and the token cannot be refreshed. Caught by
/// <c>LoggingErrorBoundary</c> so the user is redirected to login with a friendly
/// message rather than shown the generic error UI.
/// </summary>
public class SessionExpiredException : Exception
{
    public SessionExpiredException()
        : base("The user session has expired.")
    {
    }
}
