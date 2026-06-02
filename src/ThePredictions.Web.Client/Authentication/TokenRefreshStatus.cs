namespace ThePredictions.Web.Client.Authentication;

/// <summary>
/// Outcome of an attempt to obtain a valid access token, so callers can tell a
/// genuinely-ended session apart from a transient failure (e.g. the API
/// restarting during a deploy) that must NOT log the user out.
/// </summary>
public enum TokenRefreshStatus
{
    /// <summary>A valid access token was returned.</summary>
    Succeeded,

    /// <summary>The refresh token was rejected (expired/revoked/missing) — the session is over.</summary>
    InvalidSession,

    /// <summary>The refresh could not be completed (network error, 5xx, rate limit) — keep the session.</summary>
    TransientFailure
}
