namespace ThePredictions.Web.Client.Authentication;

/// <summary>
/// Carries a one-shot message from the point a session ends (e.g. an expired
/// refresh token) to the login page, so the user sees "you've been logged out"
/// instead of a raw error. Scoped, so it lives for the lifetime of the app.
/// </summary>
public class SessionState
{
    public string? LogoutMessage { get; set; }
}
